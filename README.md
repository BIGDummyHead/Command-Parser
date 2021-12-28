# BIGDummyHead's Command Parser

This library allows you to parse strings easily. Allowing you to create class instances, invoke methods, setup command modules, create custom converters, and allow for custom attributes to place over commands.

This library is very helpful for programs that expect a string input to do background work and such.


## Registering a Command module.


```csharp
using CommandParser;
using System.Reflection;

//registering a Command Module to a command handler
CommandHandler commandHandler = new CommandHandler(HandlerConfig.Default);

//we can use T types that ensure that you inheit BaseCommandModule
commandHandler.RegisterModule<BasicCommands>();

//we can then UnRegister this module
commandHandler.UnRegisterModule<BasicCommands>();

//we can also use the Type method
commandHandler.RegisterModule(typeof(BaseCommandModule)); //we can also do this with the UnRegisterModule method.

public class BasicCommands : BaseCommandModule
{
    //this method is invoked when any command is invoked.
    public override void OnCommandExecute(MethodInfo method, object instance, object[] invokes, object returnInstance)
    {
    }
}
```

## Creating commands in a module

```csharp
using CommandParser;
using System;

//in any BaseCommandModule
class BasicCommands : BaseCommandModule
{
    //we can create a method that takes in two ints and adds them together

    [Command("add")] //we will add the command attribute over top of this method for the registration
    //we can also do [Command] and this will use the name of the method and not a custom name
    public int Add(int a, int b)
    {
        return a + b;
    }

    //we can add multiple commands of the same name as well
    //the handler is smart enough to know when you are calling two different commands.
    [Command("add")]
    public void Add(string say) { Console.WriteLine(say); }
}
```

## Built-in Attributes

```csharp

using CommandParser;
using System;

class BasicCommands : BaseCommandModule
{
    //examples of special attributes
    //these are commands that are hard coded into the library

    //RemainingText

    //with this attribute you will add it only to your last paramter.
    //instead of your handler only taking in 1 argument the rest of the string will be used for the command
    [Command("say")]
    public void Say([RemainingText] string txt)
    {
        Console.WriteLine(txt);
    }

    //Ignore

    //with this attribute it will ignore any valid commands, this can be used for source control and other things.
    [Command("add")]
    [Ignore] //this command will now be ignored!
    public void Add(int a , int b) { }

    //Required Params

    //with this attribute it allows you to use extra parameters when invoking, this attribute can be placed on any parameter
    //each param will usually only allow 1 argument but with RemainingText or RequiredParam we can change this
    //note: if this attribute is placed on the last parameter and so is RemainingText, then required param will be chosen.

    [Command("say")]
    public void SayLong(string name, [RequiredParams(2)] string twoThing, [RequiredParams(3)] string threeThing) { } //therefore this command will take 6 total arguments.

}

```

## Special Attributes

```csharp

using CommandParser;

class CustomAttribute : BaseCommandAttribute //all attributes that inherit this are collected by the handler.
{

    public override bool BeforeCommandExecute(object classInstance, object[] methodParams)
    {

        //if true then the command will continue. This is determined also with the HandlerConfig.ByPopularVote
        return true;
    }

    public override void AfterCommandExecute(object classInstance, object[] methodParams, object returnInstance)
    {
    }
}

```

#### We can also use the AdvancedCommandAttribute, this allows us to modify and edit the argument array provided to the Handler.
`Note: This attribute also inherits the BaseCommandAttribute`

```csharp

class AdvancedAttr : AdvancedCommandAttribute
{
    public override void OnCollect(ParameterInfo[] pInfo, ref string[] args)
    {
	//we will make any changes to the args as we please, allowing us to modify the handler before it selects an appropriate method!
    }
}

```