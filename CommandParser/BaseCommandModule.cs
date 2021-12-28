﻿using System.Reflection;
using System.Threading.Tasks;

namespace CommandParser
{
    /// <summary>
    /// A base for all command modules
    /// </summary>
    public abstract class BaseCommandModule
    {
#nullable enable
#pragma warning disable CS1998
        /// <summary>
        /// Called when any in the module is invoked. 
        /// </summary>
        /// <param name="method">The method invoked</param>
        /// <param name="instance">The instance used to invoke the <paramref name="method"/>.</param>
        /// <param name="invokes">The parameters used to invoke the <paramref name="method"/></param>
        /// <param name="returnInstance">Return of the command if any</param>
        public virtual async Task OnCommandExecute(MethodInfo method, object instance, object[] invokes, object? returnInstance)
        {

        }
#pragma warning restore CS1998
#nullable disable
    }
}