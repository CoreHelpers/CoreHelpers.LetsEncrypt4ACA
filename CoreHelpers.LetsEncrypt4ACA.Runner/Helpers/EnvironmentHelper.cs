using System;
namespace CoreHelpers.LetsEncrypt4ACA.Runner.Helpers
{
	public static class EnvironmentHelpers
	{
        public static string ValidateParameter(string variable)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(value))
            {
                var msg = $"ERROR: Missing {variable} environment variable";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                Console.ResetColor();

                throw new Exception(msg);
            }

            return value;
        }

        public static string ReadOptionalParameter(string variable, string defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(value))
                return defaultValue;
            else
                return value;
        }
    }
}

