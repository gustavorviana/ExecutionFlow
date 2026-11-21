using ExecutionFlow.Abstractions;
using System;
using Xunit;

namespace ExecutionFlow.Tests.Abstractions
{
    /// <summary>specs/016-id-format-validation FR-001.</summary>
    public class IdFormatTests
    {
        // AC-001.1
        [Theory]
        [InlineData("order-42")]
        [InlineData("ExecutionFlow.Examples.Handlers.Recurring.DataSyncHandler")]
        [InlineData("Outer+Inner")]
        [InlineData("Ns.Handler`1[[System.String, System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea5e798]]")]
        [InlineData("12345")]
        [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
        [InlineData("Pedidos.Criação")]
        [InlineData("pedido-ação-42")]
        [InlineData("注文")]
        [InlineData("\U00020000")] // a letter outside the BMP (two chars)
        [InlineData("cafe\u0301")] // combining acute accent
        public void IsValid_ReturnsTrue_WhenIdHasOnlyAllowedCharacters(string id)
        {
            Assert.True(IdFormat.IsValid(id));
        }

        // AC-001.2
        [Theory]
        [InlineData("a\rb")]
        [InlineData("a\nb")]
        [InlineData("a\tb")]
        [InlineData("a\0b")]
        [InlineData("a:b")]
        [InlineData("a/b")]
        [InlineData("a\\b")]
        [InlineData("a\"b")]
        [InlineData("a'b")]
        [InlineData("a{b")]
        [InlineData("a}b")]
        [InlineData("a<b")]
        [InlineData("a>b")]
        [InlineData("a;b")]
        [InlineData("a@b")]
        [InlineData("a#b")]
        [InlineData("a\u202Eb")] // right-to-left override
        [InlineData("a\u200Bb")] // zero-width space
        [InlineData("a\u2028b")] // line separator
        [InlineData("a\u00A0b")] // no-break space
        [InlineData("a\uD800b")] // lone surrogate
        [InlineData("\uDC00")]   // lone low surrogate
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(" order-42 ")]
        [InlineData(" order-42")]
        [InlineData("order-42 ")]
        [InlineData(null)]
        public void IsValid_ReturnsFalse_WhenIdHasCharacterOutsideFormat(string id)
        {
            Assert.False(IdFormat.IsValid(id));
        }

        // AC-001.3
        [Fact]
        public void Validate_EscapesControlCharactersInMessage_WhenIdIsInvalid()
        {
            var ex = Assert.Throws<ArgumentException>(() => IdFormat.Validate("a\r\nb", "jobId"));

            Assert.Equal("jobId", ex.ParamName);
            Assert.Contains(@"a\r\nb", ex.Message);
            Assert.DoesNotContain("\r", ex.Message);
            Assert.DoesNotContain("\n", ex.Message);
        }

        [Fact]
        public void Validate_ThrowsArgumentNull_WhenIdIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => IdFormat.Validate(null, "jobId"));
            Assert.Equal("jobId", ex.ParamName);
        }

        [Fact]
        public void Validate_DoesNotThrow_WhenIdIsValid()
        {
            IdFormat.Validate("Pedidos.Criação+Inner", "jobId");
        }

        [Fact]
        public void ValidateRecurring_NamesHandlerAndEscapesId_WhenIdIsInvalid()
        {
            var ex = Assert.Throws<ArgumentException>(() => IdFormat.ValidateRecurring(typeof(IdFormatTests), "sync\u202Edaily", "Id"));

            Assert.Contains(typeof(IdFormatTests).FullName, ex.Message);
            Assert.Contains(@"sync\u202Edaily", ex.Message);
        }

        [Fact]
        public void Escape_KeepsSurrogatePairsAndAccents()
        {
            Assert.Equal("Criação\U00020000", IdFormat.Escape("Criação\U00020000"));
        }
    }
}
