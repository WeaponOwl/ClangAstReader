using System;
using System.Collections.Generic;
using System.Linq;
using ClangReader.Types;

namespace ClangReader
{
    class TranslationFile
    {
        public Dictionary<string, TypedefDeclaration> typedef = new Dictionary<string, TypedefDeclaration>();
        public Dictionary<string, VariableDeclaration> variables = new Dictionary<string, VariableDeclaration>();
        public Dictionary<string, FunctionDeclaration> functions = new Dictionary<string, FunctionDeclaration>();
        public Dictionary<string, StructureDeclaration> structures = new Dictionary<string, StructureDeclaration>();
        public Dictionary<string, EnumDeclaration> enums = new Dictionary<string, EnumDeclaration>();
    }

    class MainClass
    {
        public static Dictionary<string, TranslationFile> translation = new Dictionary<string, TranslationFile>();
        protected static TranslationFile GetTranslation(string className)
        {
            if (!translation.ContainsKey(className))
                translation.Add(className, new TranslationFile());
            return translation[className];
        }

        public static void Main(string[] args)
        {
            //string astDumpPath = "/home/misha/Projects/cache/functionExecute.cpp.dump";
            string astDumpPath = "F:/cache/mainSystem.cpp.dump";
            AstTextFile dumpFile = new AstTextFile(astDumpPath);

            foreach (var rootToken in dumpFile.rootTokens)
            {
                foreach (var token in rootToken.children)
                {
                    // process only non standart file source
                    if (!token.context.sourceFile.StartsWith("<", StringComparison.InvariantCulture) &&
                        !token.context.sourceFile.StartsWith("/usr", StringComparison.InvariantCulture))
                    {
                        switch (token.name)
                        {
                            case "TypedefDecl":
                                ProcessTypedef(token);
                                break;
                            case "UsingDirectiveDecl":
                                //    Console.WriteLine("    using {0}", string.Join(" ", token.properties));
                                break;
                            case "VarDecl":
                                ProcessVariableDeclaration(token);
                                break;
                            case "FunctionDecl":
                                ProcessFunctionDeclaration(token);
                                break;
                            case "EnumDecl":
                                ProcessEnum(token);
                                break;
                            case "CXXRecordDecl": // declaration of struct/class without body
                                ProcessStructDeclaration(token);
                                break;
                            case "EmptyDecl": break;
                            default: throw new NotImplementedException(token.name);
                        }
                    }
                }
            }

            translation.Clear();
            Console.WriteLine("[Wait for key press]");
            Console.ReadKey();
        }

        protected static string GetTranslatedTargetClass(string sourceFile)
        {
            if (sourceFile.StartsWith("./", StringComparison.InvariantCulture)) sourceFile = sourceFile.Substring(2);
            sourceFile = System.IO.Path.GetFileNameWithoutExtension(sourceFile);
            return char.ToUpper(sourceFile[0]) + sourceFile.Substring(1);
        }
        protected static string GetTranslatedTargetFile(string sourceFile)
        {
            if (sourceFile.StartsWith("./", StringComparison.InvariantCulture)) sourceFile = sourceFile.Substring(2);
            var extension = System.IO.Path.GetExtension(sourceFile);
            sourceFile = System.IO.Path.GetFileNameWithoutExtension(sourceFile);
            return char.ToUpper(sourceFile[0]) + sourceFile.Substring(1) + (extension == ".h" ? "Header" : "Cpp");
        }

        protected static TypeDeclaration GetTypeDeclaration(AstToken token)
        {
            switch (token.name)
            {
                case "BuiltinType":
                    return new TypeDeclaration() { name = token.properties[0], isBuildIn = true };
                case "PointerType":
                    var pointerDeclaration = GetTypeDeclaration(token.children[0]);
                    pointerDeclaration.isPointer = true;
                    return pointerDeclaration;
                case "RecordType":
                    return new TypeDeclaration() { name = token.properties[0] };
                case "ElaboratedType":  // struct
                    return GetTypeDeclaration(token.children[0]);
                case "TypedefType":
                    return new TypeDeclaration() { name = token.properties[0] };
                case "ParenType":
                    switch (token.children[0].name)
                    {
                        case "FunctionProtoType":
                            var functionDeclaration = GetFunctionProtoDeclaration(token.children[0]);
                            functionDeclaration.name = token.properties[0];
                            functionDeclaration.isBuildIn = false;
                            return functionDeclaration;
                        default: throw new NotImplementedException(token.children[0].name);
                    }
                case "QualType": // type with modificator
                    return GetTypeDeclaration(token.children[0]);
                case "EnumType": // type with modificator
                    return new TypeDeclaration() { name = token.properties[0] };
                case "...": // params
                    return new TypeDeclaration() { name = token.name };
                default: throw new NotImplementedException(token.name);
            }
        }
        protected static FunctionProtoDeclaration GetFunctionProtoDeclaration(AstToken token)
        {
            var function = new FunctionProtoDeclaration()
            {
                returnType = GetTypeDeclaration(token.children[0])
            };
            for (int i = 1; i < token.children.Count; i++)
            {
                function.parameters.Add(GetTypeDeclaration(token.children[i]));
            }
            return function;
        }

        protected static string GetValue(AstToken token)
        {
            switch (token.name)
            {
                case "InitListExpr":
                    System.Text.StringBuilder builder = new System.Text.StringBuilder();
                    builder.Append("[");
                    for (int i = 0; i < token.children.Count; i++)
                    {
                        if (i != 0) builder.Append(",");
                        builder.Append(GetValue(token.children[i]));
                    }
                    builder.Append("]");
                    return builder.ToString();

                case "ImplicitCastExpr":
                    if (token.children.Count > 1) throw new InvalidCastException();
                    return GetValue(token.children[0]);
                case "CXXBoolLiteralExpr":
                    return token.properties[1];     // property 0 is 'bool'
                case "IntegerLiteral":
                    return token.properties[1];     // property 0 is 'int'; if this one have context - this may be define
                case "GNUNullExpr": 
                    return "null";
                case "StringLiteral":
                    return token.properties[2];     // property 0 is 'char[...]', property 1 is 'lvalue'
                case "CStyleCastExpr":
                    if (token.children.Count > 1) throw new InvalidCastException();
                    return GetValue(token.children[0]);     // here probably need append cast prefix
                case "DeclRefExpr":
                    return token.properties[3];     // here need check for pointer to declaration
                case "UnaryOperator":
                    var operation = token.properties[1];
                    if (operation == "prefix") return token.properties[token.properties.Length - 1] + GetValue(token.children[0]);
                    else if (operation == "postfix") return GetValue(token.children[0]) + token.properties[token.properties.Length - 1];
                    else throw new Exception(operation);
                case "BinaryOperator":
                    return GetValue(token.children[0]) + token.properties[token.properties.Length - 1] + GetValue(token.children[1]);
                case "ParenExpr":
                    return "(" + GetValue(token.children[0]) + ")";
                default: throw new Exception(token.name);
            }
        }

        protected static VariableDeclaration GetVariableDeclaration(AstToken token)
        {
            var variable = new VariableDeclaration()
            {
                name = token.properties[0],
                type = token.properties[1],
            };

            if (token.attributes.Contains("cinit"))
            {
                variable.value = GetValue(token.children[0]);
            }

            if (token.attributes.Contains("static"))
            {
                variable.isStatic = true;
            }

            if (token.attributes.Contains("extern"))
            {
                variable.isExtern = true;
            }

            for (int i = 2; i < token.properties.Length; i++)
            {
                switch (token.properties[i])
                {
                    default: throw new Exception(token.properties[i]);
                }
            }

            return variable;
        }

        protected static string GetFunctionBody(AstToken token)
        {
            string reference;
            System.Text.StringBuilder stringBuilder;
            switch (token.name)
            {
                case "CompoundStmt":
                    stringBuilder = new System.Text.StringBuilder();
                    stringBuilder.AppendLine("{");
                    foreach (var childToken in token.children)
                        stringBuilder.AppendLine(GetFunctionBody(childToken));
                    stringBuilder.AppendLine("}");
                    return stringBuilder.ToString();
                case "ReturnStmt":
                    if(token.children.Count > 1) throw new ArgumentException();
                    if (token.children.Count == 0) return "return;";
                    return "return " + GetFunctionBody(token.children[0]) + ";";
                case "ConditionalOperator":
                    if (token.children.Count != 3) throw new ArgumentException();
                    return GetFunctionBody(token.children[0]) + "?" + GetFunctionBody(token.children[1]) + ":" + GetFunctionBody(token.children[2]);
                case "ParenExpr":
                    if (token.children.Count != 1) throw new ArgumentException();
                    return "(" + GetFunctionBody(token.children[0]) + ")";
                case "BinaryOperator":
                    return GetFunctionBody(token.children[0]) + token.properties[token.properties.Length - 1] + GetFunctionBody(token.children[1]);
                case "UnaryOperator":
                    if (token.children.Count != 1) throw new ArgumentException();
                    var operation = token.properties[1];
                    if (operation == "lvalue") operation = token.properties[2];
                    if (operation == "prefix") return token.properties[token.properties.Length - 1] + GetFunctionBody(token.children[0]);
                    else if (operation == "postfix") return GetFunctionBody(token.children[0]) + token.properties[token.properties.Length - 1];
                    else throw new ArgumentException();
                case "ImplicitCastExpr":
                    if (token.children.Count != 1) throw new ArgumentException();
                    return GetFunctionBody(token.children[0]);
                case "DeclRefExpr": // variable
                    reference = token.properties[1];
                    if (reference == "lvalue")
                        return token.properties[4]; // remove quotes
                    return token.properties[3];     // remove quotes
                case "IntegerLiteral":
                    return token.properties[1];     // property 0 is 'int'; if this one have context - this may be define
                case "CharacterLiteral":
                    return token.properties[1];      // property 0 is 'char'
                case "StringLiteral":
                    reference = token.properties[1];     // property 0 is 'char[...]'
                    if (reference == "lvalue")
                        return token.properties[2];
                    return token.properties[1];
                case "CXXBoolLiteralExpr":
                    return token.properties[1];     // property 0 is 'bool'
                case "CStyleCastExpr":
                    if (token.children.Count != 1) throw new ArgumentException();
                    return GetFunctionBody(token.children[0]);  // maybe need cast with parameter[0]
                case "MemberExpr":
                    if (token.children.Count != 1) throw new ArgumentException();
                    reference = token.properties[1];
                    if (reference == "lvalue")
                        return GetFunctionBody(token.children[0]) + token.properties[2];
                    return GetFunctionBody(token.children[0]) + token.properties[1];
                case "CallExpr":
                    if (token.children.Count== 0) throw new ArgumentException();
                    stringBuilder = new System.Text.StringBuilder();
                    stringBuilder.Append(GetFunctionBody(token.children[0]));
                    stringBuilder.Append("(");
                    for (int i = 1; i < token.children.Count; i++)
                    {
                        if (token.children[i].name == "CXXDefaultArgExpr") break;
                        if (i > 1) stringBuilder.Append(",");
                        stringBuilder.Append(GetFunctionBody(token.children[i]));
                    }
                    stringBuilder.Append(")");
                    return stringBuilder.ToString();
                case "ArraySubscriptExpr":
                    if (token.children.Count != 2) throw new ArgumentException();
                    return GetFunctionBody(token.children[0]) + "["+ GetFunctionBody(token.children[1]) + "]";

                case "DeclStmt":
                    stringBuilder = new System.Text.StringBuilder();
                    foreach (var childToken in token.children)
                        stringBuilder.AppendLine(GetFunctionBody(childToken));
                    return stringBuilder.ToString();

                case "VarDecl":
                    if (token.properties.Length != 2) throw new ArgumentException();
                    stringBuilder = new System.Text.StringBuilder();
                    stringBuilder.Append(token.properties[1]);
                    stringBuilder.Append(" ");
                    stringBuilder.Append(token.properties[0]);
                    if (token.attributes.Contains("cinit"))
                    {
                        if (token.children.Count != 1) throw new ArgumentException();
                        stringBuilder.Append(" = ");
                        stringBuilder.Append(GetFunctionBody(token.children[0]));
                    }
                    return stringBuilder.ToString()+";";

                case "WhileStmt":
                    if (token.children.Count != 3) throw new ArgumentException();
                    return "while(" + GetFunctionBody(token.children[1]) + ")\n" + GetFunctionBody(token.children[2]);

                case "IfStmt":
                    if (token.children.Count != 5) throw new ArgumentException();
                    return "if(" + GetFunctionBody(token.children[2]) + ")\n" + GetFunctionBody(token.children[3]);

                case "ForStmt":
                    if (token.children.Count != 5) throw new ArgumentException();
                    return "for(" + GetFunctionBody(token.children[0]) + ";" + GetFunctionBody(token.children[2]) + ";" + GetFunctionBody(token.children[3]) + ")\n" + GetFunctionBody(token.children[4]);

                case "GNUNullExpr": return "null";
                case "NullStmt": return ";";

                case "CompoundAssignOperator":
                    if (token.children.Count != 2) throw new ArgumentException();
                    reference = token.properties[1];
                    if (reference == "lvalue")
                        return GetFunctionBody(token.children[0]) + token.properties[2] + GetFunctionBody(token.children[1]);
                    return GetFunctionBody(token.children[0]) + token.properties[1] + GetFunctionBody(token.children[1]);

                case "UnaryExprOrTypeTraitExpr":
                    if (token.properties.Length == 3)
                        return token.properties[1] + "(" + token.properties[2] + ")";
                    if (token.children.Count != 1) throw new ArgumentException();
                    return token.properties[1] + GetFunctionBody(token.children[0]);

                case "BreakStmt": return "break;";
                case "ContinueStmt": return "continue;";
                case "GotoStmt": return "goto " + token.properties[0] + ";";
                case "LabelStmt":
                    stringBuilder = new System.Text.StringBuilder();
                    stringBuilder.Append(token.properties[0]);
                    stringBuilder.AppendLine(":");
                    foreach (var childToken in token.children)
                        stringBuilder.AppendLine(GetFunctionBody(childToken));
                    return stringBuilder.ToString();

                case "ExprWithCleanups":            // i not sure about this one
                    if (token.children.Count != 1) throw new ArgumentException();
                    return GetFunctionBody(token.children[0]);

                case "CXXConstructExpr":            // i not sure about this one
                    if (token.children.Count != 1) throw new ArgumentException();
                    return GetFunctionBody(token.children[0]);

                case "MaterializeTemporaryExpr":    // i not sure about this one
                    if (token.children.Count != 1) throw new ArgumentException();
                    return GetFunctionBody(token.children[0]);

                case "CXXMemberCallExpr":           // i not sure about this one
                    stringBuilder = new System.Text.StringBuilder();
                    stringBuilder.Append(GetFunctionBody(token.children[0]));
                    stringBuilder.Append("(");
                    for (int i = 1; i < token.children.Count; i++)
                    {
                        //if (token.children[i].name == "CXXDefaultArgExpr") break;
                        if (i > 1) stringBuilder.Append(",");
                        stringBuilder.Append(GetFunctionBody(token.children[i]));
                    }
                    stringBuilder.Append(")");
                    return stringBuilder.ToString();

                case "CXXOperatorCallExpr":           // i not sure about this one
                    if (token.children.Count == 3)
                        return GetFunctionBody(token.children[1]) + GetFunctionBody(token.children[0]) + GetFunctionBody(token.children[2]);
                    else if (token.children.Count == 2)
                        return GetFunctionBody(token.children[1]) + GetFunctionBody(token.children[0]);
                    else throw new ArgumentException();

                default: throw new Exception(token.name);
                //default: return "Unknown";
            }
        }

        protected static FunctionDeclaration GetFunctionDeclaration(AstToken token)
        {
            var function = new FunctionDeclaration()
            {
                name = token.properties[0],
            };

            foreach (var childToken in token.children)
            {
                switch (childToken.name)
                {
                    case "ParmVarDecl":
                        FunctionDeclaration.Parameter parameter = null;
                        if (childToken.properties.Length == 1)
                        {
                            parameter = new FunctionDeclaration.Parameter()
                            {
                                name = null,
                                type = childToken.properties[0],
                            };
                        }
                        else if (childToken.properties.Length == 2)
                        {
                            parameter = new FunctionDeclaration.Parameter()
                            {
                                name = childToken.properties[0],
                                type = childToken.properties[1],
                            };
                        }
                        if (childToken.attributes.Contains("cinit"))
                        {
                            parameter.value = GetValue(childToken.children[0]);
                        }

                        function.parameters.Add(parameter);
                        break;
                    case "CompoundStmt":
                        function.body = GetFunctionBody(childToken);
                        break;
                    case "FullComment": break; // ignore
                    default: throw new Exception(childToken.name);
                }
            }

            return function;
        }

        protected static StructureDeclaration GetStructDeclaration(AstToken token) 
        {
            StructureDeclaration structure = new StructureDeclaration();

            if (token.properties[0] == "union") structure.isUnion = true;
            else if (token.properties[0] == "class") structure.isClass = true;
            else if (token.properties[0] != "struct")  throw new ArgumentException();

            if (token.properties.Length > 1)
                structure.name = token.properties[1];

            foreach (var childToken in token.children)
            {
                switch (childToken.name)
                {
                    case "DefinitionData": // almost no idea how to parse it // TODO
                        break; 
                    case "CXXRecordDecl":
                        structure.subStructures.Add(GetStructDeclaration(childToken));
                        break;
                    case "FieldDecl":
                        structure.properties.Add(new StructureDeclaration.Property() {
                            name = childToken.properties[0],
                            type = childToken.properties[1]
                        });
                        break;
                    case "public":  // TODO
                        break;
                    case "FullComment": // ignore this. or TODO if you wish
                        break;
                    case "AccessSpecDecl": // TODO local modificator based on properties[0]
                        break;
                    case "CXXConstructorDecl":  // TODO
                        break;
                    case "CXXDestructorDecl":  // TODO
                        break;
                    case "CXXMethodDecl":  // TODO
                        break;
                    default: throw new Exception(childToken.name);
                }
            }

            return structure;
        }

        protected static void ProcessTypedef(AstToken token)
        {
            var targetClass = GetTranslatedTargetClass(token.context.sourceFile);
            var targetFileName = GetTranslatedTargetFile(token.context.sourceFile);
            var translationFile = GetTranslation(targetClass);

            var aliasType = GetTypeDeclaration(token.children[0]);
            translationFile.typedef.Add(token.offset, new TypedefDeclaration()
            {
                name = token.properties[0],
                alias = aliasType
            });
        }
        protected static void ProcessVariableDeclaration(AstToken token)
        {
            var targetClass = GetTranslatedTargetClass(token.context.sourceFile);
            var targetFileName = GetTranslatedTargetFile(token.context.sourceFile);
            var translationFile = GetTranslation(targetClass);

            var variable = GetVariableDeclaration(token);
            translationFile.variables.Add(token.offset, variable);

            //if (variable.value != null)
            //{
            //    Console.WriteLine(variable.name + " = " + variable.value);
            //}
        }
        protected static void ProcessEnum(AstToken token)
        {
            var targetClass = GetTranslatedTargetClass(token.context.sourceFile);
            var targetFileName = GetTranslatedTargetFile(token.context.sourceFile);
            var translationFile = GetTranslation(targetClass);

            EnumDeclaration enumDeclaration = new EnumDeclaration() { name = token.properties[0] };

            foreach (var childToken in token.children)
            {
                switch (childToken.name)
                {
                    case "EnumConstantDecl":
                        var enumConstant = new EnumDeclaration.Property(){name = childToken.properties[0]};
                        if (childToken.children.Count > 0)
                        {
                            foreach (var subChildToken in childToken.children)
                            {
                                switch (subChildToken.name)
                                {
                                    case "ImplicitCastExpr":
                                        enumConstant.value = GetValue(subChildToken);
                                        break;
                                    default: throw new Exception(subChildToken.name);
                                }
                            }
                        }
                        enumDeclaration.properties.Add(enumConstant);
                        break;
                    default: throw new Exception(childToken.name);
                }
            }

            translationFile.enums.Add(token.offset, enumDeclaration);
        }
        protected static void ProcessFunctionDeclaration(AstToken token)
        {
            var targetClass = GetTranslatedTargetClass(token.context.sourceFile);
            var targetFileName = GetTranslatedTargetFile(token.context.sourceFile);
            var translationFile = GetTranslation(targetClass);

            var function = GetFunctionDeclaration(token);
            translationFile.functions.Add(token.offset, function);

            //Console.Write(function.name + "(");
            //foreach (var param in function.parameters)
            //{
            //    Console.Write(param.type+" ");
            //    if (param.name != null)
            //        Console.Write(param.name + " ");
            //    if (param.value != null)
            //        Console.Write("= "+param.value);
            //    Console.Write(", ");
            //}
            //Console.WriteLine(")");
        }
        protected static void ProcessStructDeclaration(AstToken token)
        {
            var targetClass = GetTranslatedTargetClass(token.context.sourceFile);
            var targetFileName = GetTranslatedTargetFile(token.context.sourceFile);
            var translationFile = GetTranslation(targetClass);

            var structure = GetStructDeclaration(token);
            translationFile.structures.Add(token.offset, structure);
        }
    }
}
