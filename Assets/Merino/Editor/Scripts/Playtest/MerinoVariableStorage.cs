using System.Collections.Generic;
using UnityEngine;
using Yarn;

namespace Merino
{
    
    // this is pretty much just YarnSpinner/ExampleVariableStorage with all the MonoBehaviour stuff taken out
    public class MerinoVariableStorage : VariableStorage
    {
        /// Where we actually keeping our variables
        Dictionary<string, Yarn.Value> variables = new Dictionary<string, Yarn.Value> ();
	
        /// A default value to apply when the object wakes up, or
        /// when ResetToDefaults is called
        [System.Serializable]
        public class DefaultVariable
        {
            /// Name of the variable
            public string name;
            /// Value of the variable
            public string value;
            /// Type of the variable
            public Yarn.Value.Type type;
        }
	
        /// Our list of default variables, for debugging.
        DefaultVariable[] defaultVariables = new DefaultVariable[0];
	
        /// Reset to our default values when the game starts
        public MerinoVariableStorage ()
        {
            ResetToDefaults ();
        }
	
        /// Erase all variables and reset to default values
        public void ResetToDefaults ()
        {
            Clear ();
	
            // For each default variable that's been defined, parse the string
            // that the user typed in in Unity and store the variable
            foreach (var variable in defaultVariables) {
				
                object value;
	
                switch (variable.type) {
                    case Yarn.Value.Type.Number:
                        float f = 0.0f;
                        float.TryParse(variable.value, out f);
                        value = f;
                        break;
	
                    case Yarn.Value.Type.String:
                        value = variable.value;
                        break;
	
                    case Yarn.Value.Type.Bool:
                        bool b = false;
                        bool.TryParse(variable.value, out b);
                        value = b;
                        break;
	
                    case Yarn.Value.Type.Variable:
                        // We don't support assigning default variables from other variables
                        // yet
                        MerinoDebug.LogFormat(LoggingLevel.Error,
                            "Can't set variable {0} to {1}: You can't set a default variable to be another variable, because it may not have been initialised yet.",
                            variable.name, variable.value);
                        continue;
	
                    case Yarn.Value.Type.Null:
                        value = null;
                        break;
	
                    default:
                        throw new System.ArgumentOutOfRangeException ();
	
                }
	
                var v = new Yarn.Value(value);
	
                SetValue ("$" + variable.name, v);
            }
        }
			
        /// Set a variable's value
        public void SetNumber (string variableName, float value)
        {
            // Copy this value into our list
            variables[variableName] = new Yarn.Value(value);
        }
	
        /// Get a variable's value
        public float GetNumber (string variableName)
        {
            // If we don't have a variable with this name, return the null value
            if (variables.ContainsKey(variableName) == false)
                return -1f;
		
            return variables [variableName].AsNumber;
        }
	
        /// Set a variable's value
        public void SetValue (string variableName, Yarn.Value value)
        {
            // Copy this value into our list
            variables[variableName] = new Yarn.Value(value);
        }
	
        /// Get a variable's value
        public Yarn.Value GetValue (string variableName)
        {
            // If we don't have a variable with this name, return the null value
            if (variables.ContainsKey(variableName) == false)
                return Yarn.Value.NULL;
			
            return variables [variableName];
        }
	
        /// Erase all variables
        public void Clear ()
        {
            variables.Clear ();
        }
			
    }
}