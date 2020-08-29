using System;
using System.Collections.Generic;

namespace ChildToNPC.Integrations.ContentPatcher
{
    /// <summary>A child token in the structure recognized by Content Patcher.</summary>
    internal class ChildToken
    {
        /*********
        ** Fields
        *********/
        private readonly Func<bool> UpdateContextImpl;
        private readonly Func<bool> IsReadyImpl;
        private readonly Func<string> GetValuesImpl;


        /*********
        ** Public methods
        *********/
        public ChildToken(Func<bool> updateContext, Func<bool> isReady, Func<string> getValue)
        {
            this.UpdateContextImpl = updateContext ?? throw new ArgumentNullException(nameof(updateContext));
            this.IsReadyImpl = isReady ?? throw new ArgumentNullException(nameof(isReady));
            this.GetValuesImpl = getValue ?? throw new ArgumentNullException(nameof(getValue));
        }

        public bool IsReady()
        {
            return this.IsReadyImpl();
        }

        public bool UpdateContext()
        {
            return this.UpdateContextImpl();
        }

        public IEnumerable<string> GetValues(string input)
        {
            string value = this.GetValuesImpl.Invoke();
            if (value != null)
                return new[] { value };

            return new string[0];
        }
    }
}
