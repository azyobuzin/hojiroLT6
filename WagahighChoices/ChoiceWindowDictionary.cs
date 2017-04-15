using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace WagahighChoices
{
    public class ChoiceWindowDictionary
    {
        private static readonly JsonSerializer s_serializer = JsonSerializer.Create();

        public string FileName { get; }
        private readonly Dictionary<string, ChoiceWindowInfo> _dic;

        private ChoiceWindowDictionary(string fileName, Dictionary<string, ChoiceWindowInfo> dic)
        {
            this.FileName = fileName;
            this._dic = dic;
        }

        public static ChoiceWindowDictionary Load(string fileName)
        {
            using (var reader = new JsonTextReader(new StreamReader(fileName)))
            {
                var dic = s_serializer.Deserialize<Dictionary<string, ChoiceWindowInfo>>(reader);
                return new ChoiceWindowDictionary(fileName, dic);
            }
        }

        public void Save()
        {
            using (var writer = new StreamWriter(this.FileName))
                s_serializer.Serialize(writer, this._dic);
        }

        private static string ToHex(byte[] bs)
        {
            var sb = new StringBuilder(bs.Length * 2);
            foreach (var b in bs)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public bool Add(byte[] hash, ChoiceWindowInfo value)
        {
            var key = ToHex(hash);
            if (this._dic.ContainsKey(key)) return false;

            this._dic.Add(key, value);
            return true;
        }

        public ChoiceWindowInfo Get(byte[] hash)
        {
            this._dic.TryGetValue(ToHex(hash), out var value);
            return value;
        }
    }
}
