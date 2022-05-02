﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using StringParser.Interfaces;

namespace StringParser;

/// <summary>
/// A class that can intake a prefix, name, and arguments and parse them into a invoked command.
/// </summary>
public sealed class Handler
{
    internal readonly Dictionary<MethodInfo, CollectedCommand> _command = new();
    internal readonly Dictionary<Type, ICommandModule> _modules = new();

    /// <summary>
    /// Commands being invoked.
    /// </summary>
    public IReadOnlyDictionary<MethodInfo, CollectedCommand> Commands => _command;

    /// <summary>
    /// <see cref="ICommandModule"/>(s) registered.
    /// </summary>
    public IReadOnlyDictionary<Type, ICommandModule> Modules => _modules;

    /// <summary>
    /// Options for your Handler
    /// </summary>
    public HandlerConfig Config { get; init; }

    /// <summary>
    /// The used converter.
    /// </summary>
    /// <para><see cref="StringConverter"/> is provided by default.</para>
    public IStringConverter Converter { get; set; } = new StringConverter();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="config"></param>
    public Handler(HandlerConfig config)
    {
        Config = config;
    }

    /// <summary>
    /// Create a command handler with the <see cref="HandlerConfig.Default"/>
    /// </summary>
    public Handler() : this(HandlerConfig.Default)
    {

    }

    /// <summary>
    /// Invoke a command, must start with the prefix - name - arguments
    /// <para>Example: !name arg1 arg2</para>
    /// </summary>
    /// <param name="invoker"></param>
    /// <returns>The result of the method if any.</returns>
    /// <exception cref="Exceptions.InvalidConversionException"></exception>
    public async Task<object?> Invoke(string invoker)
    {
        return await Invoke(Array.Empty<object>(), invoker, Array.Empty<object>());
    }

    /// <summary>
    /// Invoke a command, must start with the prefix - name - arguments
    /// <para>Example: !name arg1 arg2</para>
    /// </summary>
    /// <param name="invoker"></param>
    /// <param name="aft">Non-string object arguments that are supplied after the string args.</param>
    /// <returns>The result of the method if any.</returns>
    /// <exception cref="Exceptions.InvalidConversionException"></exception>
    public async Task<object?> Invoke(string invoker, params object[] aft)
    {
        return await Invoke(Array.Empty<object>(), invoker, aft);
    }

    /// <summary>
    /// Invoke a command, must start with the prefix - name - arguments
    /// <para>Example: !name arg1 arg2</para>
    /// </summary>
    /// <param name="pre">Non-string object arguments that are supplied before the string args.</param>
    /// <param name="invoker">Invoking string arguments</param>
    /// <param name="aft">Non-string object arguments that are supplied after the string args.</param>
    /// <returns>The result of the method if any.</returns>
    /// <exception cref="Exceptions.InvalidConversionException"></exception>
    public async Task<object?> Invoke(object[] pre, string invoker, object[] aft)
    {
        if (invoker.Length < Config.Prefix.Length)
        {
            Config.ToLog("Invalid invocation, too short", LogLevel.Error);
            return null;
        }

        string prefix = Config.HasPrefix ? invoker[0..(Config.Prefix.Length)] : string.Empty;

        if (Config.HasPrefix)
        {
            if (!prefix.Equals(Config.Prefix, Config.Comp))
            {
                Config.ToLog($"Prefix invalid! Expected '{Config.Prefix}'", LogLevel.Information);
                return null;
            }
        }

        string[] stringArgs = invoker.Split(Config.Separator);

        string commandName = stringArgs[0][prefix.Length..];

        stringArgs = stringArgs[1..];

        MethodInfo method = null;

        var filteredCommands = _command.Where(x => x.Value.Name.Equals(commandName, Config.Comp));

        if (!filteredCommands.Any())
        {
            string b = $"There is no command with the name of '{commandName}'";

            foreach (var item in _command)
            {
                b += $"\r\n* {item.Key.Name}";
            }

            Config.ToLog(b, LogLevel.Warning);
            return null;
        }

        foreach (KeyValuePair<MethodInfo, CollectedCommand> selCommand in filteredCommands)
        {
            CollectedCommand command = selCommand.Value;

            foreach (ParameterInfo pi in command.parameters)
            {
                if (!command.parameterAttributes.TryGetValue(pi, out IEnumerable<CommandParameterAttribute> _cpas))
                    continue;

                foreach (CommandParameterAttribute cpa in _cpas)
                {
                    cpa.Handler = this;
                    stringArgs = await cpa.OnCollect(pi, pre, stringArgs, aft, command.parameters);
                }
            }

            if (command.parameters.Length == pre.Length + stringArgs.Length + aft.Length)
            {
                method = command.method;
                break;
            }
        }

        int stringArgCount = stringArgs.Length;

        int totalCount = pre.Length + stringArgCount + aft.Length;

        if (method == null)
        {
            Config.ToLog($"No command with the name of '{commandName}' has '{totalCount}' arguments.", LogLevel.Warning);
            return null;
        }

        List<object> lso = new();

        lso.AddRange(pre);

        for (int i = 0; i < stringArgCount; i++)
        {
            int at = pre.Length + i;
            string arg = stringArgs[i];

            Type type = method.GetParameters()[at].ParameterType;

            if (!Converter.CastString(pre, arg, aft, type, out ValueTask<object> converted, out string error))
            {
                Config.ToLog(error, LogLevel.Error);
                return null;
            }

            lso.Add(await converted);
        }

        lso.AddRange(aft);

        CollectedCommand finalInfoCommand = _command[method];

        IEnumerable<BaseCommandAttribute> bcas = method.GetCustomAttributes<BaseCommandAttribute>();

        object[] invokeArr = lso.ToArray();

        int yay = 0;
        int nay = 0;

        foreach (BaseCommandAttribute bca in bcas)
        {
            bca.Handler = this;
            if (await bca.BeforeCommandExecute(finalInfoCommand.instance, invokeArr))
                yay++;
            else
                nay++;
        }

        if (yay >= nay && Config.ByPopularVote || !Config.ByPopularVote)
        {
            object? result = method.Invoke(finalInfoCommand.instance, invokeArr);

            await this.Modules[method.DeclaringType].OnCommandExecute(method, finalInfoCommand.instance, invokeArr, result);

            foreach (BaseCommandAttribute bca in bcas)
                await bca.AfterCommandExecute(finalInfoCommand.instance, invokeArr, result);

            return result;
        }

        Config.ToLog($"Popular vote decided not to invoke '{commandName}' for method : '{method.Name}'", LogLevel.Information);
        return null;
    }

    /// <summary>
    /// Register a type with <see cref="CommandAttribute"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="Exceptions.InvalidModuleException"></exception>
    /// <exception cref="Exceptions.CommandExistException"></exception>
    public void RegisterModule<T>() where T : ICommandModule
    {
        RegisterModule(typeof(T));
    }

    /// <summary>
    /// Register a type with <see cref="CommandAttribute"/>, must inherit <seealso cref="ICommandModule"/>
    /// </summary>
    /// <param name="reg"></param>
    /// <exception cref="Exceptions.InvalidModuleException"></exception>
    /// <exception cref="Exceptions.CommandExistException"></exception>
    public void RegisterModule(Type reg)
    {
        if (!reg.Inherits(typeof(ICommandModule)))
            throw new Exceptions.InvalidModuleException(reg, $"does not inherit '{typeof(ICommandModule).Name}.");
        else if (reg.GetConstructor(Array.Empty<Type>()) == null || reg.IsAbstract || reg.IsInterface)
            throw new Exceptions.InvalidModuleException(reg, "does not have an empty constructor, or an instance of it can not be made.");

        MethodInfo[] typeMethods = reg.GetMethods((BindingFlags)(-1)); //get all methods of all kinds

        //create an instance for invoking later on down the line
        object? i = Activator.CreateInstance(reg);

        if (i is null)
            throw new Exceptions.InvalidModuleException(reg, "could not be created because an empty CTOR does not exist.");


        (i as ICommandModule).UsedHandler = this;

        foreach (MethodInfo method in typeMethods)
        {
            CommandAttribute cmd;
            if (method.GetCustomAttribute<IgnoreAttribute>() is null &&
                (cmd = method.GetCustomAttribute<CommandAttribute>()) is not null)
            {

                AddCommand(cmd, i, method);
                cmd.OnRegister(reg, method);
            }
        }

        _modules.Add(reg, (ICommandModule)i);
    }

    /// <summary>
    /// Fully unregisters a module
    /// </summary>
    /// <param name="unreg"></param>
    public void UnRegisterModule(Type unreg)
    {
        if (!unreg.Inherits(typeof(ICommandModule)))
            return;
        else if (!_modules.ContainsKey(unreg))
            return;

        foreach (MethodInfo method in unreg.GetMethods((BindingFlags)(-1)))
        {
            if (_command.ContainsKey(method))
            {
                _command[method].cmdAttr.OnUnRegister(unreg, method);
                _command.Remove(method);
            }
        }

        _modules.Remove(unreg);
    }

    /// <summary>
    /// Fully unregisters a module
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void UnRegisterModule<T>() => UnRegisterModule(typeof(T));



    private void AddCommand(CommandAttribute cmd, object instance, MethodInfo info)
    {
        if (cmd.UsingMethodName)
            cmd.CommandName = info.Name;

        CollectedCommand commandInfo = new(cmd.CommandName, cmd, instance, info);

        //both _commands and _instances contain the same keys
        foreach (KeyValuePair<MethodInfo, CollectedCommand> command in Commands)
        {
            if (command.Value == commandInfo)
                throw new Exceptions.CommandExistException(commandInfo.Name);
        }

        _command.Add(info, commandInfo);
    }
}

/// <summary>
/// Specific info about a command, for allowing more commands
/// </summary>
public struct CollectedCommand
{
    /// <summary>
    /// Name provided 
    /// </summary>
    public string Name { get; internal set; }
    /// <summary>
    /// Amount of arguments to invoke the method info
    /// </summary>
    public int ParameterCount { get; internal set; }

    internal CollectedCommand(string name, CommandAttribute cmdAttr, object instance, MethodInfo method)
    {
        Name = name;

        this.cmdAttr = cmdAttr;
        this.instance = instance;
        this.method = method;
        isIgnored = method.GetCustomAttribute<IgnoreAttribute>() != null;
        parameters = method.GetParameters();

        ParameterCount = parameters.Length;

        List<KeyValuePair<ParameterInfo, IEnumerable<CommandParameterAttribute>>> ls = new();
        foreach (ParameterInfo pi in parameters)
        {
            var cpa = pi.GetCustomAttributes<CommandParameterAttribute>();

            if (cpa == null)
                continue;

            ls.Add(new(pi, cpa));
        }

        parameterAttributes = new Dictionary<ParameterInfo, IEnumerable<CommandParameterAttribute>>(ls);
        ls.GetEnumerator().Dispose();

    }

    /// <summary>
    /// The <see cref="CommandAttribute"/> of the command
    /// </summary>
    public readonly CommandAttribute cmdAttr;
    /// <summary>
    /// The instance of the type.
    /// </summary>
    public readonly object instance;
    /// <summary>
    /// The method to invoke.
    /// </summary>
    public readonly MethodInfo method;

    /// <summary>
    /// Is the method ignored.
    /// </summary>
    public readonly bool isIgnored;

    /// <summary>
    /// Parameters collected from the method.
    /// </summary>
    public readonly ParameterInfo[] parameters;

    /// <summary>
    /// <see cref="CommandParameterAttribute"/>(s) from the <seealso cref="method"/>.
    /// </summary>

    public readonly IReadOnlyDictionary<ParameterInfo, IEnumerable<CommandParameterAttribute>> parameterAttributes;


    /// <summary>
    /// Checks if the left and right have the same name and parameter count.
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    public static bool operator ==(CollectedCommand left, CollectedCommand right)
    {
        if (string.IsNullOrEmpty(left.Name) || string.IsNullOrEmpty(right.Name))
            return false;

        return left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase) && left.ParameterCount == right.ParameterCount;
    }

    /// <summary>
    /// Checks if the left and right do not have the same name and parameter count.
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    public static bool operator !=(CollectedCommand left, CollectedCommand right)
    {
        return !(left == right);
    }

    /// <summary>
    /// </summary>
    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
        if (obj.GetType() != typeof(CollectedCommand))
            return false;

        return this == (CollectedCommand)obj;
    }

}