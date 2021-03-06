﻿using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Scaffolding
{
    public class SqlServerCodeGeneratorTest
    {
        [Fact]
        public virtual void Use_provider_method_is_generated_correctly()
        {
            var codeGenerator = new NpgsqlCodeGenerator(new ProviderCodeGeneratorDependencies());

            var result = codeGenerator.GenerateUseProvider("Server=test;Username=test;Password=test;Database=test");

            Assert.Equal("UseNpgsql", result.Method);
            Assert.Collection(
                result.Arguments,
                a => Assert.Equal("Server=test;Username=test;Password=test;Database=test", a));
        }
    }
}
