using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

//Compaore Yann Djamel 
namespace SerializationAndDeserializationApp
{
    public class JsonSerializer
    {
        public string Serialize(object obj)
        {
            var stringBuilder = new StringBuilder();
            SerializeObject(obj, stringBuilder);
            return stringBuilder.ToString();
        }

        private void SerializeObject(object obj, StringBuilder stringBuilder)
        {
            if (obj == null)
            {
                stringBuilder.Append("null");
                return;
            }

            var type = obj.GetType();

            if (IsSimpleType(type))
            {
                AppendSimpleValue(obj, stringBuilder);
            }
            else if (type.IsArray || obj is IEnumerable)
            {
                SerializeArray(obj, stringBuilder);
            }
            else
            {
                SerializeComplexObject(obj, stringBuilder);
            }
        }

        private bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
        }

        private void AppendSimpleValue(object obj, StringBuilder stringBuilder)
        {
            if (obj is string)
            {
                stringBuilder.Append($"\"{obj}\"");
            }
            else if (obj is bool)
            {
                stringBuilder.Append(obj.ToString().ToLower());
            }
            else
            {
                stringBuilder.Append(obj);
            }
        }

        private void SerializeArray(object obj, StringBuilder stringBuilder)
        {
            var enumerable = obj as IEnumerable;
            stringBuilder.Append("[");
            var isFirst = true;

            foreach (var item in enumerable)
            {
                if (!isFirst)
                {
                    stringBuilder.Append(",");
                }

                SerializeObject(item, stringBuilder);
                isFirst = false;
            }

            stringBuilder.Append("]");
        }
        private void SerializeComplexObject(object obj, StringBuilder stringBuilder)
        {
            stringBuilder.Append("{");
            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            var isFirst = true;

            foreach (var property in properties)
            {
                var excludeAttribute = property.GetCustomAttribute<ExcludeAttribute>();
                if (excludeAttribute != null && excludeAttribute.Exclude)
                {
                    continue; // Ignorer les propriétés exclues
                }

                if (!isFirst)
                {
                    stringBuilder.Append(",");
                }

                stringBuilder.Append($"\"{property.Name}\":");
                SerializeObject(property.GetValue(obj), stringBuilder);
                isFirst = false;
            }

            stringBuilder.Append("}");
        }
    }

    //Mame Bara Diop
    public class JsonDeserializer
    {
        public T Deserialize<T>(string jsonString)
        {
            var jsonObject = ParseJsonObject(jsonString);
            var obj = Activator.CreateInstance<T>();

            foreach (var property in typeof(T).GetProperties())
            {
                var propertyName = property.Name;
                if (jsonObject.ContainsKey(propertyName))
                {
                    var propertyValue = jsonObject[propertyName];
                    if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                    {
                        var complexObj = DeserializeComplexObject(property.PropertyType, propertyValue.ToString());
                        property.SetValue(obj, complexObj);
                    }
                    else if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var listType = property.PropertyType.GetGenericArguments()[0];
                        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(listType));

                        foreach (var item in (List<object>)propertyValue)
                        {
                            if (listType.IsClass && listType != typeof(string))
                            {
                                var listItem = DeserializeComplexObject(listType, item.ToString());
                                list.Add(listItem);
                            }
                            else
                            {
                                var convertedItem = Convert.ChangeType(item, listType);
                                list.Add(convertedItem);
                            }
                        }

                        property.SetValue(obj, list);
                    }
                    else
                    {
                        var convertedValue = Convert.ChangeType(propertyValue, property.PropertyType);
                        property.SetValue(obj, convertedValue);
                    }
                }
            }

            return obj;
        }

        private Dictionary<string, object> ParseJsonObject(string jsonString)
        {
            var jsonObject = new Dictionary<string, object>();

            // Supprime les espaces et les caractères de nouvelle ligne
            jsonString = jsonString.Replace("\n", "").Replace("\r", "").Replace("\t", "").Trim();

            // Supprime les accolades environnantes
            jsonString = jsonString.TrimStart('{').TrimEnd('}');

            // Divisé en paires clé-valeur
            var keyValuePairs = SplitKeyValuePairs(jsonString);

            foreach (var pair in keyValuePairs)
            {
                var keyValue = pair.Split(':');

                // Vérifiez si la paire clé-valeur est valide
                if (keyValue.Length != 2)
                {
                    // Gère la paire clé-valeur invalide

                    continue;
                }

                // Supprime les guillemets environnants et la touche de découpage
                var key = keyValue[0].Replace("\"", "").Trim();

                // Valeur de coupe
                var value = keyValue[1].Trim();

                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    // Gère la valeur de la chaîne
                    value = value.Trim('"');
                    jsonObject[key] = value;
                }
                else if (value == "null")
                {
                    // Gère la valeur nulle
                    jsonObject[key] = null;
                }
                else if (value.StartsWith("[") && value.EndsWith("]"))
                {
                    // Gérer la valeur du tableau (en supposant que le tableau contienne des chaînes)
                    var arrayValues = value.TrimStart('[').TrimEnd(']').Split(',');
                    var trimmedArray = arrayValues.Select(arr => arr.Trim('"')).ToList();
                    jsonObject[key] = trimmedArray;
                }
                else if (value.StartsWith("{") && value.EndsWith("}"))
                {
                    // Gérer l'objet imbriqué (analyse récursive)
                    var nestedObj = value.TrimStart('{').TrimEnd('}');
                    jsonObject[key] = ParseNestedObject(nestedObj);
                }
                else
                {
                    // Gère d'autres types simples (int, bool, etc.)
                    jsonObject[key] = value;
                }
            }

            return jsonObject;
        }

        // Cette méthode pour diviser correctement les paires clé-valeur
        private List<string> SplitKeyValuePairs(string input)
        {
            var keyValuePairs = new List<string>();
            var buffer = new StringBuilder();
            var isInQuotes = false;

            foreach (char c in input)
            {
                if (c == '"')
                {
                    isInQuotes = !isInQuotes;
                }

                if (c == ',' && !isInQuotes)
                {
                    keyValuePairs.Add(buffer.ToString());
                    buffer.Clear();
                }
                else
                {
                    buffer.Append(c);
                }
            }

            if (buffer.Length > 0)
            {
                keyValuePairs.Add(buffer.ToString());
            }

            return keyValuePairs;
        }

        private Dictionary<string, object> ParseNestedObject(string nestedObject)
        {
            var nestedJsonObject = ParseJsonObject(nestedObject);
            return nestedJsonObject;
        }

        private object DeserializeComplexObject(Type type, string jsonString)
        {
            var jsonObject = ParseJsonObject(jsonString);
            var complexObj = Activator.CreateInstance(type);

            foreach (var property in type.GetProperties())
            {
                var propertyName = property.Name;
                if (jsonObject.ContainsKey(propertyName))
                {
                    var propertyValue = jsonObject[propertyName];

                    // Gérer les objets imbriqués de manière récursive
                    if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                    {
                        var nestedObj = DeserializeComplexObject(property.PropertyType, propertyValue.ToString());
                        property.SetValue(complexObj, nestedObj);
                    }
                    else
                    {
                        // Gérer d'autres types de propriétés
                        var convertedValue = Convert.ChangeType(propertyValue, property.PropertyType);
                        property.SetValue(complexObj, convertedValue);
                    }
                }
            }

            return complexObj;
        }
    }
    [AttributeUsage(AttributeTargets.Property)]

    //Adel Chebani
    public class ExcludeAttribute : Attribute
    {
        public bool Exclude { get; }

        public ExcludeAttribute(bool exclude = true)
        {
            Exclude = exclude;
        }
    }
    public class Residence
    {
        public string Street { get; set; }
        [Exclude]
        public string City { get; set; }
    }
    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public Residence Residence { get; set; }
        public List<string> Hobbies { get; set; }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var person = new Person
            {
                Name = "John",
                Age = 30,
                Residence = new Residence { Street = "123 Main St", City = "Exemple" },
                Hobbies = new List<string> { "Lire", "Jardinage" }
            };

            var serializer = new JsonSerializer();
            var deserializer = new JsonDeserializer();

            // Sérialise l'objet en JSON

            string json = serializer.Serialize(person);
            Console.WriteLine("JSON sérialisé:");
            Console.WriteLine(json);

            // Désérialise JSON en objet
            var deserializedPerson = deserializer.Deserialize<Person>(json);
            Console.WriteLine("\nObjet désérialisé :");
            Console.WriteLine($"Name: {deserializedPerson.Name}, Age: {deserializedPerson.Age}");
            Console.WriteLine("Residence:");
            Console.WriteLine($"Street: {person.Residence.Street}");
            Console.WriteLine("Hobbies:");
            foreach (var hobby in person.Hobbies)
            {
                Console.WriteLine(hobby);
            }
            Console.ReadLine();
        }
    }
}
