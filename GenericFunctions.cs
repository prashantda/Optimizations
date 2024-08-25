 public class GenericFunctions
    {


        #region Functions for Replacing
        public static async Task<StringBuilder> ReplaceKeyWithValue(StringBuilder source,
                  JObject requestJObject, string bussiness, Dictionary<string, string> resolvedConditions)
        {
            if (source == null || source.Length == 0 || requestJObject == null || bussiness == null)
            {
                return source;
            }

            JObject configJObject = null;
            try
            {
                //Reading the config file according to bussiness
                string path = Path.Combine(Directory.GetCurrentDirectory(), "ConfigFiles", bussiness + ".json");
                string jsonContent = File.ReadAllText(path);
                configJObject = JObject.Parse(jsonContent);
                configJObject = (JObject)configJObject["KeyValues"];

            }
            catch { configJObject = null; }

            try
            {
                //Finding Key from html content
                StringBuilder result = new StringBuilder();
                string inputString = Convert.ToString(source);
                int i = 0;
                while (i < inputString.Length)
                {
                    if (inputString[i] == '#' && i + 1 < inputString.Length && inputString[i + 1] == '#')
                    {
                        i += 2;
                        StringBuilder keyBuilder = new StringBuilder();
                        while (i < inputString.Length && !(inputString[i] == '#' && i + 1 < inputString.Length && inputString[i + 1] == '#'))
                        {
                            keyBuilder.Append(inputString[i]);
                            i++;
                        }
                        i++;
                        string key = Convert.ToString(keyBuilder);

                        string stringres = GetResponse(requestJObject, configJObject, key, resolvedConditions);

                        if (stringres != null)
                        {
                            result.Append(stringres);
                        }
                        else
                        {
                            result.Append("##" + key + "##");
                        }
                    }
                    else
                    {
                        result.Append(inputString[i]);
                    }
                    i++;
                }


                result = GenericRemove("##RemoveHTMLContent##", result);

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                throw new ArgumentNullException("Some Error occured in Generic Replace method. " + ex.Message);
            }
        }

        public static string GetResponse(JObject requestJObject, JObject configJObject, string key, Dictionary<string, string> resolvedConditions)
        {
            string stringres = null;
            JObject obj = null;
            if (configJObject != null)
            {
                obj = (JObject)configJObject[key];
            }
            if (obj != null)
            {
                stringres = GetValueFromConfig(obj, requestJObject, key, (JObject)configJObject["Conditions"], resolvedConditions).Result;
            }
            if (stringres == null)
            {
                stringres = (string)requestJObject[key];
            }
            if (stringres == null)
            {
                stringres = CheckForCondition(configJObject, requestJObject, key);
            }
            return stringres;
        }

        public static async Task<string> GetValueFromConfig(JObject obj, JObject requestJObject, string key, JObject conditions, Dictionary<string, string> resolvedConditions)
        {
            string stringres = null;
            if (obj == null || requestJObject == null || key == null)
            {
                return null;
            }
            try
            {

                string KeyConfig = Convert.ToString(obj["type"]);
                if (KeyConfig != null)
                {
                    switch (KeyConfig)
                    {
                        case "Different_Key_Value":
                            {
                                if (obj["ActualProperty"] != null)
                                {
                                    string actual = obj["ActualProperty"].ToString();

                                    stringres = Convert.ToString(requestJObject[actual]);
                                }
                                break;
                            }
                        case "Nested":
                            {
                                if (obj["ActualProperty"] != null)
                                {
                                    string actual = obj["ActualProperty"].ToString();

                                    stringres = Convert.ToString(requestJObject.SelectToken(actual));
                                }
                                break;
                            }
                        case "Plain":
                            {
                                stringres = Convert.ToString(obj["ActualValue"]);

                                break;
                            }
                        case "RemoveContent":
                            {
                                stringres = "##RemoveHTMLContent##";
                                break;
                            }
                        case "Condition":
                            {
                                stringres = GetResponseForConditions((JObject)obj["Condition"], requestJObject, key, conditions, resolvedConditions);
                                break;
                            }
                        default:
                            {
                                stringres = null;
                                break;
                            }
                    }

                }
                return await Task.FromResult(stringres);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Functions For Conditions
        public static string GetResponseForConditions(JObject obj, JObject requestJObject, string key, JObject conditions, Dictionary<string, string> resolvedConditions)
        {
            string response = null;
            string condition = Convert.ToString(obj["Type"]);
            switch (condition)
            {
                case "If":
                    {
                        response = GetIfResponse(obj, requestJObject, conditions, resolvedConditions);
                        break;
                    }
                case "Switch":
                    {
                        response = GetSwitchResponse(obj, requestJObject);
                        break;
                    }
            }

            return response;
        }
        public static string GetSwitchResponse(JObject obj, JObject requestJObject)
        {
            string basedOn = Convert.ToString(obj.SelectToken("BasedOn"));
            string switchValue = Convert.ToString(requestJObject.SelectToken(basedOn));
            if (string.IsNullOrEmpty(switchValue))
            {
                return null;
            }
            string response = null;
            JArray jArray = obj["Options"] as JArray;
            string[] sArray = jArray.ToObject<string[]>();
            JArray jValueArray = obj["ReplaceWith"] as JArray;
            string[] sValueArray = jValueArray.ToObject<string[]>();
            for (int i = 0; i < sArray.Length; i++)
            {
                if (sArray[i].Equals(switchValue))
                {
                    response = sValueArray[i]; break;
                }
            }
            string valueType = Convert.ToString(obj.SelectToken("ReplaceType"));
            response = valueType.Equals("Resolve") ? Convert.ToString(requestJObject.SelectToken(response)) : response;
            return response;
        }
        public static string GetIfResponse(JObject obj, JObject requestJObject, JObject conditions, Dictionary<string, string> resolvedConditions)
        {
            JObject[] allConditions = obj["AllConditions"].ToObject<JObject[]>();
            int i = 0;
            bool shouldReplaceOrNot = false;
            for (; i < allConditions.Length; i++)
            {
                shouldReplaceOrNot = SatisfyAllConditions(allConditions[i]["Conditions"] as JArray, conditions, requestJObject, resolvedConditions);
                if (shouldReplaceOrNot)
                {
                    break;
                }
            }

            if (shouldReplaceOrNot)
            {
                obj = allConditions[i];
                string valueType = Convert.ToString(obj["ValueType"]);
                string value = Convert.ToString(obj["ReplaceWith"]);
                return valueType.Equals("Resolve") ? Convert.ToString(requestJObject.SelectToken(value)) : value;
            }
            return null;
        }
        public static bool SatisfyAllConditions(JArray conditionsJArray, JObject conditions, JObject requestJObject, Dictionary<string, string> resolvedConditions)
        {
            string[] conditionsArray = conditionsJArray.ToObject<string[]>();
            string firstParam, secondParam, lhsResolve, rhsResolve, relation;
            string[] firstParamArray, secondParamArray, lhsResolveArray, rhsResolveArray, relationArray;
            bool result = true, temp = false;
            JObject conditionalObject = null;
            for (int i = 0; i < conditionsArray.Length; i++)
            {

                if (resolvedConditions.TryGetValue(conditionsArray[i], out string value))
                {
                    temp = Convert.ToBoolean(value);
                }
                else
                {

                    if (!result) break;
                    temp = false;
                    conditionalObject = conditions[conditionsArray[i]] as JObject;
                    firstParamArray = GetStringsFromJArray(conditionalObject["LHS"] as JArray);
                    secondParamArray = GetStringsFromJArray(conditionalObject["RHS"] as JArray);
                    lhsResolveArray = GetStringsFromJArray(conditionalObject["LHS_Type"] as JArray);
                    rhsResolveArray = GetStringsFromJArray(conditionalObject["RHS_Type"] as JArray);
                    relationArray = GetStringsFromJArray(conditionalObject["Relation"] as JArray);

                    for (int j = 0; j < firstParamArray.Length; j++)
                    {
                        firstParam = firstParamArray[j]; secondParam = secondParamArray[j];
                        lhsResolve = lhsResolveArray[j]; rhsResolve = rhsResolveArray[j];
                        relation = relationArray[j];
                        temp = SatisfyConditionOrNot(firstParam, secondParam, lhsResolve, rhsResolve, relation, requestJObject);
                        if (temp)
                        {
                            break;
                        }
                    }
                    resolvedConditions.Add(conditionsArray[i], Convert.ToString(temp));

                }
                if (temp && result)
                {
                    result = true;
                }
                else
                {
                    result = false;
                }

            }

            return result;
        }
        public static bool SatisfyConditionOrNot(string firstParam, string secondParam, string lhsResolve, string rhsResolve, string relation, JObject requestJObject)
        {
            firstParam = lhsResolve switch
            {
                "Resolve" => Convert.ToString(requestJObject.SelectToken(firstParam)),
                "GetLength" => GetLength(requestJObject, firstParam),
                _ => firstParam
            };
            secondParam = rhsResolve switch
            {
                "Resolve" => Convert.ToString(requestJObject.SelectToken(secondParam)),
                "GetLength" => GetLength(requestJObject, secondParam),
                _ => secondParam
            };

            switch (relation)
            {
                case "EqualTo": return firstParam.Equals(secondParam);
                case "NotEqualTo": return !firstParam.Equals(secondParam);
                case "IsLessThan": return Convert.ToInt32(firstParam) < Convert.ToInt32(secondParam);
                case "IsMoreThan": return Convert.ToInt32(firstParam) > Convert.ToInt32(secondParam);
                case "EqualToIgnoreCase": return string.Equals(firstParam, secondParam, StringComparison.OrdinalIgnoreCase);
                case "NotEqualToIgnoreCase": return !string.Equals(firstParam, secondParam, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
        public static string CheckForCondition(JObject configJObject, JObject requestJObject, string key)
        {

            var keyIndex = GetKeyNIndex(key);
            if (!string.IsNullOrEmpty(keyIndex.Value))
            {
                return GetForResponse(configJObject, requestJObject, keyIndex);
            }
            else
            {
                return null;
            }
        }
        public static KeyValuePair<string, string> GetKeyNIndex(string key)
        {
            StringBuilder sb = new StringBuilder();
            int i = key.Length - 1;
            for (; i >= 0; i--)
            {
                if ((int)key[i] > 47 && (int)key[i] < 58)
                {
                    sb.Append(key[i]);
                }
                else
                {
                    break;
                }
            }
            return new KeyValuePair<string, string>
                            (key.Substring(0, i + 1),
                            string.Join("", sb.ToString().Reverse()));
        }
        public static string GetForResponse(JObject configJObject, JObject requestJObject, KeyValuePair<string, string> keyIndex)
        {

            configJObject = configJObject[keyIndex.Key] as JObject;
            if (configJObject == null)
            { return null; }
            bool shouldReplace = SatisfyForLoop(configJObject["Condition"] as JObject, requestJObject, keyIndex.Value);
            if (!shouldReplace)
            {
                return null;
            }

            string replaceWith = Convert.ToString(configJObject["ReplaceWith"]);
            string valueType = Convert.ToString(configJObject["ValueType"]);
            string str = Convert.ToString(configJObject["Expression"]);

            if (string.IsNullOrEmpty(str))
            {
                return replaceWith;
            }
            str = str.Replace("i", keyIndex.Value);
            string finalIndex = EvaluateString(str);
            int.TryParse(finalIndex, out int index);
            return ResolveForCondition(requestJObject, index, valueType, replaceWith);

        }

        public static string ResolveForCondition(JObject requestJObject, int index, string valueType, string replaceWith)
        {
            if (!replaceWith.Contains("i"))
            {
                return (Convert.ToString(GetValueForDiffrentPropertyName(requestJObject, replaceWith).Value));
            }
            int firstIndex = replaceWith.IndexOf('[');
            int lastIndex = replaceWith.LastIndexOf(".");
            string firstParam = replaceWith.Substring(0, firstIndex);
            string secondParam = replaceWith.Substring(lastIndex + 1);
            return GetValueFromRequest(requestJObject, index, firstParam, secondParam);
        }

        public static string GetValueFromRequest(JObject requestJObject, int index, string firstParam, string secondParam)
        {
            JArray jArray = GetValueForDiffrentPropertyName(requestJObject, firstParam).Value as JArray;
            JObject jObject = jArray[index] as JObject;
            return Convert.ToString(GetValueForDiffrentPropertyName(jObject, secondParam).Value);
        }

        public static bool SatisfyForLoop(JObject configJObject, JObject requestJObject, string index)
        {
            int.TryParse(index, out int ind);
            var start = Convert.ToString(configJObject["Start"]);
            var end = Convert.ToString(configJObject["ShouldBeLessThan"]);
            var endType = Convert.ToString(configJObject["EndType"]);
            end = endType switch
            {
                "GetLength" => GetLength(requestJObject, end),
                "Evaluate" => EvaluateString(end),
                _ => end
            };
            int.TryParse(end, out int endInInt);
            int.TryParse(start, out int startInInt);
            return ind >= startInInt && ind < endInInt;

        }
        public static string EvaluateString(string expression)
        {
            DataTable table = new DataTable();
            object result = null;

            try
            {
                result = table.Compute(expression, "");
            }
            catch
            {
                result = null;
            }


            return Convert.ToString(result);
        }
        #endregion

        #region Helper Functions

        public static string[] GetStringsFromJArray(JArray jArray)
        {
            return jArray.ToObject<string[]>();
        }
        public static string GetLength(JObject requestJObject, string firstParam)
        {
            string param = firstParam.Split('.')[0];
            JProperty jToken = GetValueForDiffrentPropertyName(requestJObject, param);
            JArray jArray = jToken.Value as JArray;
            int lengthOfArray = jArray.Count;
            return Convert.ToString(lengthOfArray);
        }
        public static JProperty GetValueForDiffrentPropertyName(JObject requestJObject, string param)
        {
            return requestJObject.Properties().FirstOrDefault(p => p.Name.Equals(param, StringComparison.OrdinalIgnoreCase));
        }

        #endregion



        #region Functions For Removing Content From whole StringBuilder
        //Method for deleting all key sections at once
        public static StringBuilder RemoveContentsWithKeys(StringBuilder target, params string[] keys)
        {
            if (target == null || keys.Length == 0)
            {
                return target;
            }
            foreach (string key in keys)
            {
                target = GenericRemove(key, target);
            }
            return target;
        }


        //Use Method to remove multiple content of given key
        public static StringBuilder GenericRemove(string key, StringBuilder target)
        {
            if (target == null || target.Length == 0 || string.IsNullOrEmpty(key))
            {
                return null;
            }
            try
            {

                int count = 0, position = 0;

                while ((position = target.ToString().IndexOf(key, position, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    count++;
                    position += key.Length;
                }
                for (int i = 0; i < count; i++)
                {
                    target = RemoveContentBetweenTags(key, target);
                }
                return target;

            }
            catch (Exception ex)
            {
                throw new ArgumentException("Some Error Occured in GenericRemove Function " + ex.Message);
            }

        }

        //To remove single content of given key
        public static StringBuilder RemoveContentBetweenTags(string key, StringBuilder target)
        {
            if (string.IsNullOrEmpty(key) || target == null || target.Length == 0)
            {
                throw new ArgumentException("Error in RemoveContentBetweenTags method");
            }
            string str = target.ToString();
            int start = str.IndexOf(key);
            if (start == -1)
            {
                return target; // Key not found
            }

            int openingTagIndex = FindOpeningTagIndex(str, start);
            if (openingTagIndex == -1)
            {
                return target; // Invalid HTML structure
            }

            string tagName = GetTagName(str, openingTagIndex - 1);
            HashSet<string> tags = new HashSet<string>() { "area", "base", "br", "col",
                "command", "embed", "hr", "img", "input","keygen", "link", "meta", "param", "source", "track", "wbr"};
            if (tags.Contains(tagName))
            {
                int closingIndex = FindClosingTagClosingIndex(str, openingTagIndex + 1);
                closingIndex += 1;
                target.Remove(openingTagIndex, closingIndex - openingTagIndex);
            }
            else
            {
                int closingTagOpeningIndex = FindClosingTagOpeningIndex(str, tagName, openingTagIndex + tagName.Length);
                if (closingTagOpeningIndex == -1)
                {
                    return target; // Invalid HTML structure
                }
                int closingTagClosingIndex = FindClosingTagClosingIndex(str, closingTagOpeningIndex);
                closingTagClosingIndex += 1;
                // Remove content between opening and closing tags
                target.Remove(openingTagIndex, closingTagClosingIndex - openingTagIndex);
            }



            return target;
        }

        public static int FindOpeningTagIndex(string target, int start)
        {
            for (int i = start; i >= 0; i--)
            {
                if (target[i] == '<')
                {
                    return i;
                }
            }

            return -1;
        }

        public static string GetTagName(string target, int openingTagIndex)
        {
            int Start = openingTagIndex + 1;
            int tagStart = Start;
            StringBuilder tagNameBuilder = new StringBuilder();

            for (int i = Start; i < target.Length; i++, tagStart++)
            {
                char currentChar = target[i];
                if (char.IsLetter(currentChar))
                {
                    break;
                }
            }
            for (int i = tagStart; i < target.Length; i++)
            {
                char currentChar = target[i];

                if (char.IsLetter(currentChar))
                {
                    tagNameBuilder.Append(currentChar);
                }
                else if (currentChar == ' ')
                {
                    break;
                }
                else if (!char.IsLetter(currentChar))
                {
                    break;
                }
            }

            string tagName = Convert.ToString(tagNameBuilder);
            return tagName.ToLower();
        }

        public static int GetBracketIndex(string target, int startIndex)
        {
            int a = target.IndexOf('<', startIndex);
            while (target[a + 1] == '!')
            {
                a = target.IndexOf('<', a + 1);
            }
            return a;
        }

        public static dynamic IsSameAsOpening(string target, string tag, int bracketIndex)
        {
            bool isClosing = false;
            for (int i = bracketIndex + 1; i < target.Length; i++, bracketIndex++)
            {
                if (target[i] == ' ')
                {
                    continue;
                }
                if (char.IsLetter(target[i]))
                {
                    break;
                }
                if (target[i] == '/')
                {
                    isClosing = true;
                    break;
                }
            }
            string currentTag = GetTagName(target, bracketIndex - 1);
            if (isClosing)
            {

                if (currentTag == tag)
                {
                    return true;
                }
            }
            return currentTag;
        }

        public static int FindClosingTagOpeningIndex(string target, string tagName, int start)
        {
            int count = 0;
            int closingTagIndexOpening = -1;
            int openingIndex = start + 1;
            bool flag = true;

            while (openingIndex < target.Length && flag)
            {
                openingIndex = GetBracketIndex(target, openingIndex);
                var result = IsSameAsOpening(target, tagName, openingIndex);

                if (result is bool isClosing)
                {
                    if (isClosing && count == 0)
                    {
                        closingTagIndexOpening = openingIndex;
                        flag = false;
                    }
                    else if (isClosing)
                    {
                        count--;
                    }
                }
                else if (result is string currentTag)
                {
                    if (currentTag.Equals(tagName))
                    {
                        count++;
                    }
                }

                openingIndex++;
            }

            return closingTagIndexOpening;
        }

        public static int FindClosingTagClosingIndex(string target, int start)
        {

            for (int i = start; i < target.Length; i++, start++)
            {
                char a = target[i];
                if (a == '>')
                {
                    break;
                }
            }
            return start;
        }


        #endregion

        #region NOT USED--> Functions to use when key is found in Generic Replace Function to skip indexes
        /*public static StringBuilder RemoveTagsFromResult(StringBuilder result)
        {
            string? resultString = Convert.ToString(result);
            int openingTagIndex = FindOpeningTagIndex(resultString ?? "", result.Length - 1);
            if (openingTagIndex < 0)
            {
                return result;
            }
            result.Remove(openingTagIndex, result.Length - openingTagIndex);
            return result;
        }
        public static int GetClosingIndex(string target, int start)
        {
            int index = 0;
            int openingTagIndex = FindOpeningTagIndex(target, start);
            string tagName = GetTagName(target, openingTagIndex);
            HashSet<string> tags = new HashSet<string>() { "area", "base", "br", "col",
                "command", "embed", "hr", "img", "input","keygen", "link", "meta", "param", "source", "track", "wbr"};
            if (tags.Contains(tagName))
            {
                index = FindClosingTagClosingIndex(target, openingTagIndex + 1);
            }
            else
            {
                int closingTagOpeningIndex = FindClosingTagOpeningIndex(target, tagName, openingTagIndex + tagName.Length);
                if (closingTagOpeningIndex == -1)
                {
                    return -1;
                }
                index = FindClosingTagClosingIndex(target, closingTagOpeningIndex);

            }
            return index;
        }
*/
        #endregion

    }