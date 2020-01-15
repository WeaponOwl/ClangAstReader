using System;
using System.Linq;

namespace ClangReader
{
    public class AstTokenContext
    {
        public string sourceFile;
        public int line;
        public int column;
    }

    public class AstToken
    {
        public string name;

        public string offset;
        public string relationOffset;
        public string fileContext;
        public string filePointer;

        public AstTokenContext context;
        public string[] properties = new string[0];
        public string[] attributes = new string[0];

        public System.Collections.Generic.List<AstToken> children = new System.Collections.Generic.List<AstToken>();
        public AstToken parent;

        public override string ToString()
        {
            return name;
        }
    }

    public class AstTextFile
    {
        public string filename;
        public System.Collections.Generic.List<AstToken> rootTokens;

        public AstTextFile(string astDumpPath)
        {
            this.filename = astDumpPath;

            string[] lines = System.IO.File.ReadAllLines(astDumpPath);

            rootTokens = new System.Collections.Generic.List<AstToken>();
            System.Collections.Generic.List<AstToken> currentTokens = new System.Collections.Generic.List<AstToken>();

            AstTokenContext contextFilename = new AstTokenContext() { sourceFile = "<invalid sloc>", column = 0, line = 0 };
            foreach (var line in lines)
            {
                int lineDepth = 0;
                int tokenStart = -1;
                int tokenEnd = -1;

                for (int i = 0; i < line.Length; i += 2)
                {
                    if (line[i] == '|' || line[i] == '`' || line[i] == ' ' || line[i] == '-')
                    {
                        lineDepth++;
                        continue;
                    }
                    else
                    {
                        tokenStart = i;
                        break;
                    }
                }

                tokenEnd = line.IndexOf(' ', tokenStart);
                string token = "";
                string declaration = "";
                if (tokenEnd == -1) token = line.Substring(tokenStart, line.Length - tokenStart);
                else
                {
                    token = line.Substring(tokenStart, tokenEnd - tokenStart);
                    declaration = line.Substring(tokenEnd + 1);
                }

                AstToken astToken = this.ParseTokenDescription(token, declaration, ref contextFilename);
                if (lineDepth == 0)
                {
                    rootTokens.Add(astToken);
                    if (currentTokens.Count == 0) currentTokens.Add(astToken);
                    else currentTokens[0] = astToken;
                }
                else
                {
                    if (lineDepth >= currentTokens.Count) currentTokens.Add(astToken);
                    else currentTokens[lineDepth] = astToken;

                    currentTokens[lineDepth - 1].children.Add(astToken);
                    astToken.parent = currentTokens[lineDepth - 1];
                }
            }
        }

        protected string GetSufix(string source)
        {
            if (source[source.Length - 1] == '>') return source;
            for (int i = source.Length - 1; i >= 0; i--)
            {
                if (char.IsUpper(source[i])) return source.Substring(i);
            }
            return source;
        }
        protected string[] GetStringArray(string source)
        {
            System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();

            int depth = 0;
            bool quotedSingle = false;
            bool quotedDouble = false;
            while (source.Length > 0)
            {
                bool tryAgain = false;
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] == '<' && !quotedDouble && !quotedSingle) depth++;
                    else if (source[i] == '>' && !quotedDouble && !quotedSingle && depth > 0) depth--;
                    else if (source[i] == '\'' && !quotedDouble)
                    {
                        if (quotedSingle) depth--;
                        else depth++;
                        quotedSingle = !quotedSingle;
                    }
                    else if (source[i] == '\"' && !quotedSingle)
                    {
                        if (quotedDouble) depth--;
                        else depth++;
                        quotedDouble = !quotedDouble;
                    }
                    else if (source[i] == ' ' && depth == 0)
                    {
                        string subvalue = source.Substring(0, i);
                        parts.Add(subvalue);

                        source = source.Substring(i + 1);
                        tryAgain = true;
                        break;
                    }
                }

                if (tryAgain) continue;
                parts.Add(source);
                source = "";
            }

            return parts.ToArray();
        }

        protected AstToken ParseTokenDescription(string name, string description, ref AstTokenContext contextFilename)
        {
            var token = new AstToken() { name = name };
            string[] parameters = this.GetStringArray(description);
            int parameterStartIndex = 0;

            string sufix = this.GetSufix(name);
            switch (sufix)
            {
                case "Decl":
                    token.offset = parameters[0];
                    parameterStartIndex = 1;
                    token.fileContext = parameters[parameterStartIndex];
                    while (!token.fileContext.StartsWith("<"))
                    {
                        if (token.relationOffset != null)
                            token.relationOffset = token.relationOffset + " " + parameters[parameterStartIndex] + " " + parameters[parameterStartIndex + 1];
                        else
                            token.relationOffset = parameters[parameterStartIndex] + " " + parameters[parameterStartIndex + 1];
                        parameterStartIndex += 2;

                        token.fileContext = parameters[parameterStartIndex];
                    }

                    token.filePointer = parameters[parameterStartIndex + 1];
                    parameterStartIndex += 2;
                    break;
                case "Type":
                case "Record":
                case "Typedef":
                case "Parm":
                case "Specialization":
                case "Function":
                case "Enum":
                case "Field":
                case "Alias":
                case "Comment":
                    token.offset = parameters[0];
                    parameterStartIndex = 1;
                    break;
                case "Attr":
                case "Expr":
                case "Literal":
                case "Operator":
                case "Stmt":
                case "Cleanups":
                    token.offset = parameters[0];
                    token.fileContext = parameters[1];
                    parameterStartIndex = 2;
                    break;
                case "Data":
                case "Constructor":
                case "Assignment":
                case "Destructor":
                case "Argument":
                case "Initializer":
                case "public":
                case "private":
                case "protected":
                case "virtual":
                case "<<<NULL>>>":
                case "Overrides:":
                case "...":
                    break;
                case "original":
                    token.name = name + parameters[0];
                    token.offset = parameters[1];
                    parameterStartIndex = 2;
                    break;
                default: throw new NotImplementedException(name);
            }

            token.properties = parameters.Where((value, index) => index >= parameterStartIndex).ToArray();

            if (token.fileContext != null)
            {
                string fileContext = token.fileContext.Substring(1, token.fileContext.Length - 2);

                if (fileContext.Contains(",")) fileContext = fileContext.Substring(0, fileContext.IndexOf(","));

                string[] parts = fileContext.Split(':');

                if (parts[0] == "<invalid sloc>")
                {
                    contextFilename.sourceFile = "<invalid sloc>";
                    contextFilename.column = 0;
                    contextFilename.line = 0;
                }
                else if (parts[0] == "<built-in>")
                {
                    contextFilename.sourceFile = "<built-in>";
                    contextFilename.line = int.Parse(parts[1]);
                    contextFilename.column = int.Parse(parts[2]);
                }
                else if (parts[0] == "<scratch space>")
                {
                    contextFilename.sourceFile = "<built-in>";
                    contextFilename.line = int.Parse(parts[1]);
                    contextFilename.column = int.Parse(parts[2]);
                }
                else if (parts[0] == "line")
                {
                    contextFilename.line = int.Parse(parts[1]);
                    contextFilename.column = int.Parse(parts[2]);
                }
                else if (parts[0] == "col")
                {
                    contextFilename.column = int.Parse(parts[1]);
                }
                else
                {
                    contextFilename.sourceFile = parts[0];
                    contextFilename.line = int.Parse(parts[1]);
                    contextFilename.column = int.Parse(parts[2]);
                }

                token.context = new AstTokenContext()
                {
                    column = contextFilename.column,
                    line = contextFilename.line,
                    sourceFile = contextFilename.sourceFile
                };
            }

            // remove service properties

            if (token.properties.Length > 0 && token.properties[0] == "implicit")
            {
                token.attributes = token.attributes.Concat(new string[] { token.properties[0] }).ToArray();
                token.properties = token.properties.Where((value, index) => index >= 1).ToArray();
            }

            if (token.properties.Length > 0 && token.properties[0] == "used")
            {
                token.attributes = token.attributes.Concat(new string[] { token.properties[0] }).ToArray();
                token.properties = token.properties.Where((value, index) => index >= 1).ToArray();
            }

            if (token.properties.Length > 0 && token.properties[0] == "referenced")
            {
                token.attributes = token.attributes.Concat(new string[] { token.properties[0] }).ToArray();
                token.properties = token.properties.Where((value, index) => index >= 1).ToArray();
            }

            if (token.properties.Length > 0 && token.properties[token.properties.Length - 1] == "cinit")
            {
                token.attributes = token.attributes.Concat(new string[] { token.properties[token.properties.Length - 1] }).ToArray();
                token.properties = token.properties.Where((value, index) => index < token.properties.Length - 1).ToArray();
            }

            if (token.properties.Length > 0 && token.properties[token.properties.Length - 1] == "static")
            {
                token.attributes = token.attributes.Concat(new string[] { token.properties[token.properties.Length - 1] }).ToArray();
                token.properties = token.properties.Where((value, index) => index < token.properties.Length - 1).ToArray();
            }

            if (token.properties.Length > 0 && token.properties[token.properties.Length - 1] == "extern")
            {
                token.attributes = token.attributes.Concat(new string[] { token.properties[token.properties.Length - 1] }).ToArray();
                token.properties = token.properties.Where((value, index) => index < token.properties.Length - 1).ToArray();
            }

            if (token.properties.Length > 0 && token.properties[token.properties.Length - 1] == "callinit")
            {
                token.attributes = token.attributes.Concat(new string[] { token.properties[token.properties.Length - 1] }).ToArray();
                token.properties = token.properties.Where((value, index) => index < token.properties.Length - 1).ToArray();
            }

            if (token.properties.Length > 0 && token.properties[token.properties.Length - 1] == "definition")
            {
                token.attributes = token.attributes.Concat(new string[] { token.properties[token.properties.Length - 1] }).ToArray();
                token.properties = token.properties.Where((value, index) => index < token.properties.Length - 1).ToArray();
            }

            return token;
        }
    }
}
