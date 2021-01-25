using System;
using Xunit;

namespace CSharpFixes
{
    public class TestEdits
    {
        [Fact]
        public void TestXmlReordering()
        {
            var input = $@"<summary>
Loads the Q# data structures in a referenced assembly.
</summary>
<param name=""asm"">The Uri of the assembly to load.</param>
<remarks>
Generates suitable diagostics if <paramref name=""asm""/> could not be found or its content could not be loaded.
Catches any thrown exception, and calls <paramref name=""onException""/>, if not null.
</remarks>
<exception cref=""ArgumentException"">Something bad happened.</exception>
<param name=""onDiagnostic"">Called on all generated diagnostics.</param>
<!-- this next param is cool! -->
<param name=""onException"">Called with any exceptions thrown.</param>
<returns>Something cool.</returns>
<!-- this is a trailing comment -->";

            var expected = $@"<summary>
Loads the Q# data structures in a referenced assembly.
</summary>
<param name=""asm"">The Uri of the assembly to load.</param>
<param name=""onDiagnostic"">Called on all generated diagnostics.</param>
<!-- this next param is cool! -->
<param name=""onException"">Called with any exceptions thrown.</param>
<returns>Something cool.</returns>
<exception cref=""ArgumentException"">Something bad happened.</exception>
<remarks>
Generates suitable diagostics if <paramref name=""asm"" /> could not be found or its content could not be loaded.
Catches any thrown exception, and calls <paramref name=""onException"" />, if not null.
</remarks>
<!-- this is a trailing comment -->";

            string actual;
            Edits.TryEditReorderTags(CSharpCommentRewriter.DefaultTagOrdering, input, out actual);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestXmlReorderingCommentComments()
        {
            var input = $@"<summary>
Loads the Q# data structures in a referenced assembly.
</summary>
<!-- TODO: testing -->";

            var expected = $@"<summary>
Loads the Q# data structures in a referenced assembly.
</summary>
<!-- TODO: testing -->";

            string actual;
            Edits.TryEditReorderTags(CSharpCommentRewriter.DefaultTagOrdering, input, out actual);

            Assert.Equal(expected, actual);
        }
    }
}
