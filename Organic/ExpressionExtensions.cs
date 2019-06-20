using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Organic
{
	public partial class Assembler
	{
		private void LoadInternalExpressionExtensions()
		{
			// isref(label)
			ExpressionExtensions.Add("isref", (string value) =>
			{
				if (ReferencedValues.Contains(value.ToLower()))
					return 1;
				return 0;
			});

			// eval(expr)
			ExpressionExtensions.Add("eval", (string value) =>
			{
				var result = ParseExpression(value).Value;
				//Console.WriteLine($"eval({value}) => {result}");
				return ParseExpression(value).Value;
			});
		}
	}
}
