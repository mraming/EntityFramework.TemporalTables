using System;

namespace EntityFramework.TemporalTables.Utilities {
    internal class Check {
        public static T NotNull<T>(T value, string parameterName) where T : class {
            if (value == null) {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        public static T? NotNull<T>(T? value, string parameterName) where T : struct {
            if (value == null) {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        public static string NotEmpty(string value, string parameterName) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException($"The argument '{parameterName}' cannot be null, empty or contain only white space.");
            }

            return value;
        }
    }
}
