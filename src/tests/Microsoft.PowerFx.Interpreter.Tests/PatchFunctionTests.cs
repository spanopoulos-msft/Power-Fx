// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerFx.Core.App.ErrorContainers;
using Microsoft.PowerFx.Core.Binding;
using Microsoft.PowerFx.Core.Errors;
using Microsoft.PowerFx.Core.Functions;
using Microsoft.PowerFx.Core.Functions.FunctionArgValidators;
using Microsoft.PowerFx.Core.Localization;
using Microsoft.PowerFx.Core.Tests;
using Microsoft.PowerFx.Core.Types;
using Microsoft.PowerFx.Core.Utils;
using Microsoft.PowerFx.Functions;
using Microsoft.PowerFx.Interpreter;
using Microsoft.PowerFx.Syntax;
using Microsoft.PowerFx.Types;
using Xunit;
using static Microsoft.PowerFx.Core.Localization.TexlStrings;

namespace Microsoft.PowerFx.Interpreter.Tests
{
    public class PatchFunctionTests : PowerFxTest
    {
        private readonly ParserOptions _opts = new ParserOptions
        {
            AllowsSideEffects = true
        };

        [Theory]
        [InlineData(typeof(PatchFunction))]
        public async Task CheckArgsTestAsync(Type type)
        {
            var expressionError = new ExpressionError()
            {
                Kind = ErrorKind.ReadOnlyValue, Severity = ErrorSeverity.Critical, Message = "Something went wrong"
            };

            FormulaValue[] args = new[]
            {
                FormulaValue.NewError(expressionError)
            };

            var function = Activator.CreateInstance(type) as IAsyncTexlFunction;
            var result = await function.InvokeAsync(args, CancellationToken.None).ConfigureAwait(false);

            Assert.IsType<ErrorValue>(result);
        }

        [Fact]
        public async Task PatchFunction_LazyTable()
        {
            // how we use lazy types in PAD
            var rt = new CustomTypeRecordType(typeof(CustomPadType).FullName);
            rt.SetTypeProperties(new Dictionary<string, FormulaType>
            {
                ["Prop1"] = FormulaType.String, ["Prop2"] = FormulaType.String
            });

            var customTypeAsTable = rt.ToTable();

            PowerFxConfig config = new PowerFxConfig(Features.PowerFxV1);
            config.SymbolTable.EnableMutationFunctions();
            RecalcEngine engine = new RecalcEngine(config);

            var st = new SymbolTable();
            st.AddVariable("NewVar", customTypeAsTable, mutable: true);
            var checkResult = engine.Check("Patch(NewVar, Index(NewVar, 1), {Prop2: \"haha\"})", symbolTable: st);

            Assert.True(checkResult.IsSuccess);
        }

        public class CustomTypeRecordType : RecordType
        {
            public CustomTypeRecordType(string typeName)
            {
                TypeName = typeName;
            }

            public string TypeName { get; set; }

            private readonly IDictionary<string, FormulaType> _fieldTypes = new Dictionary<string, FormulaType>();

            public void SetTypeProperties(IDictionary<string, FormulaType> typeProperties)
            {
                foreach (var kvp in typeProperties)
                {
                    _fieldTypes[kvp.Key] = kvp.Value;
                    Add(kvp.Key, kvp.Value);
                }
            }

            public override IEnumerable<string> FieldNames => _fieldTypes.Select(field => field.Key);

            public override RecordType Add(NamedFormulaType field)
            {
                _fieldTypes[field.Name] = field.Type;
                return this;
            }

            public override bool TryGetFieldType(string name, out FormulaType type)
            {
                return _fieldTypes.TryGetValue(name, out type);
            }

            public override bool Equals(object other)
            {
                return other is CustomTypeRecordType otherType &&
                       TypeName.Equals(otherType.TypeName, StringComparison.InvariantCultureIgnoreCase);
            }

            public override int GetHashCode()
            {
                return TypeName.GetHashCode();
            }
        }
    }

    public class CustomPadType
    {
        public string Prop1 { get; set; }

        public string Prop2 { get; set; }
    }
}
