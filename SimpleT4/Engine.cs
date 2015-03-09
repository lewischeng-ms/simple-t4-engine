using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SimpleT4
{
    public class Engine
    {
        private readonly string _ttFile;
        private readonly string _csFile;

        private StreamReader _ttReader;
        private StreamWriter _csWriter;

        private readonly StringBuilder _mainBuilder = new StringBuilder();
        private readonly StringBuilder _classBuilder = new StringBuilder();

        private int _current;
        private string _propertyName;
        private string _propertyValue;

        private readonly List<string> _usings = new List<string>();
        private string _outputFile;

        /// <summary>
        /// false - building main
        /// true - building class
        /// </summary>
        private bool _buildingClass;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="ttFile">The input template file (.tt)</param>
        /// <param name="csFile">The output intermediate assembly file (.cs)</param>
        public Engine(string ttFile, string csFile)
        {
            _ttFile = ttFile;
            _csFile = csFile;
        }

        public void Transform()
        {
            using (_ttReader = new StreamReader(_ttFile))
            {
                // Advance to the first character.
                Advance();

                while (!_ttReader.EndOfStream)
                {
                    if (IsStartOfDirectiveOrControlBlock())
                    {
                        ReadDirectiveOrControlBlock();
                    }
                    else
                    {
                        ReadTextBlock();
                    }

                    // Workaround to avoid empty Write(s).
                    SkipWhiteSpace();
                }
            }

            using (_csWriter = new StreamWriter(_csFile))
            {
                WriteUsings();
                WriteClassHeader();
                WriteMain();
                WriteClassFeatures();
                WriteClassFooter();
            }
        }

        private bool IsStartOfDirectiveOrControlBlock()
        {
            if (_current == '<' && _ttReader.Peek() == '#')
            {
                Advance(); // Skip '<'
                Advance(); // Skip '#'
                return true;
            }
            
            return false;
        }

        private bool IsEndOfDirectiveOrControlBlock()
        {
            if (_current == '#' && _ttReader.Peek() == '>')
            {
                Advance(); // Skip '#'
                Advance(); // Skip '>'
                return true;
            }

            return false;
        }

        private bool Advance()
        {
            return (_current = _ttReader.Read()) >= 0;
        }

        private void ReadDirectiveOrControlBlock()
        {
            switch (_current)
            {
            case '@':
                Advance();
                ReadDirective();
                break;

            case '=':
                Advance();
                ReadControlBlock(OnExpressionControlBlock);
                break;

            case '+':
                Advance();
                _buildingClass = true;
                ReadControlBlock(OnClassFeatureControlBlock);
                break;

            default:
                _buildingClass = false;
                ReadControlBlock(OnStandardControlBlock);
                break;
            }
        }

        private void ReadDirective()
        {
            var directive = ReadIdentifier();

            switch (directive)
            {
            case "template":
                ReadTemplateDirective();
                break;

            case "assembly":
                ReadAssemblyDirective();
                break;

            case "import":
                ReadImportDirective();
                break;

            case "output":
                ReadOutputDirective();
                break;
            }
        }

        private void ReadControlBlock(Action<string> handler)
        {
            var builder = new StringBuilder();
            builder.Append((char)_current);

            while (Advance())
            {
                if (IsEndOfDirectiveOrControlBlock())
                {
                    handler(builder.ToString());
                    return;
                }

                builder.Append((char)_current);
            }
        }

        private void ReadProperty()
        {
            _propertyName = ReadIdentifier();
            Advance(); // Skip '='.
            _propertyValue = ReadQuotedString();
            SkipWhiteSpace(); // Skip white spaces after a property.
        }

        private void ReadTemplateDirective()
        {
            while (!IsEndOfDirectiveOrControlBlock())
            {
                ReadProperty();

                switch (_propertyName)
                {
                case "debug":
                    Console.WriteLine("Include debugging info (#line directives): {0}", _propertyValue);
                    break;

                case "hostspecific":
                    Console.WriteLine("Use specific host of transformation engine: {0}", _propertyValue);
                    break;

                case "language":
                    Console.WriteLine("Language of intermediate assembly: {0}", _propertyValue);
                    break;
                }
            }
        }

        private void ReadAssemblyDirective()
        {
            while (!IsEndOfDirectiveOrControlBlock())
            {
                ReadProperty();

                switch (_propertyName)
                {
                case "name":
                    Console.WriteLine("Reference added to intermediate assembly: {0}", _propertyValue);
                    break;
                }
            }
        }

        private void ReadImportDirective()
        {
            while (!IsEndOfDirectiveOrControlBlock())
            {
                ReadProperty();

                switch (_propertyName)
                {
                case "namespace":
                    Console.WriteLine("Using namespace: {0}", _propertyValue);
                    _usings.Add(_propertyValue);
                    break;
                }
            }
        }

        private void ReadOutputDirective()
        {
            while (!IsEndOfDirectiveOrControlBlock())
            {
                ReadProperty();

                switch (_propertyName)
                {
                case "extension":
                    Console.WriteLine("Output file extension: {0}", _propertyValue);
                    // Replace ".tt" with the extension specified.
                    _outputFile = _ttFile.Substring(0, _ttFile.Length - 3) + _propertyValue;
                    Console.WriteLine("Output file: {0}", _outputFile);
                    break;
                }
            }
        }

        private void SkipWhiteSpace()
        {
            while (Char.IsWhiteSpace((char)_current))
            {
                Advance();
            }
        }

        private string ReadIdentifier()
        {
            SkipWhiteSpace();

            var builder = new StringBuilder();
            while (Char.IsLetter((char)_current))
            {
                builder.Append((char)_current);
                Advance();
            }

            return builder.ToString();
        }

        private string ReadQuotedString()
        {
            Advance(); // Skip opening quote

            var builder = new StringBuilder();
            while (_current != '"')
            {
                builder.Append((char)_current);
                Advance();
            }

            Advance(); // Skip closing quote

            return builder.ToString();
        }

        private void ReadTextBlock()
        {
            var builder = new StringBuilder();
            builder.Append((char)_current);

            while (Advance())
            {
                if (IsStartOfDirectiveOrControlBlock())
                {
                    OnTextBlock(builder.ToString());
                    ReadDirectiveOrControlBlock();
                    return;
                }

                if (_current == '\"')
                {
                    // Add an extra quote.
                    builder.Append('\"');
                }

                builder.Append((char)_current);
            }

            OnTextBlock(builder.ToString());
        }

        private void OnTextBlock(string text)
        {
            var builder = _buildingClass ? _classBuilder : _mainBuilder;
            builder.AppendFormat(@"        _writer.Write(@""{0}"");
", text);
        }

        private void OnStandardControlBlock(string text)
        {
            _mainBuilder.Append(text);
        }

        private void OnClassFeatureControlBlock(string text)
        {
            _classBuilder.Append(text);
        }

        private void OnExpressionControlBlock(string text)
        {
            var builder = _buildingClass ? _classBuilder : _mainBuilder;
            builder.AppendFormat(@"        _writer.Write({0});
", text);
        }

        private void WriteUsings()
        {
            foreach (var usingEntry in _usings)
            {
                _csWriter.WriteLine("using {0};", usingEntry);
            }
        }

        private void WriteClassHeader()
        {
            _csWriter.WriteLine(
@"
public class Generator {
    private StreamWriter _writer;
");
        }

        private void WriteMain()
        {
            _csWriter.WriteLine(
@"    public void _Main() {{ using (_writer = new StreamWriter(@""{0}"")) {{
{1}    }}}}", _outputFile, _mainBuilder);
        }

        private void WriteClassFeatures()
        {
            _csWriter.WriteLine(_classBuilder.ToString());
        }

        private void WriteClassFooter()
        {
            _csWriter.WriteLine(
@"    public static void Main() { new Generator()._Main(); }
}");
        }
    }
}