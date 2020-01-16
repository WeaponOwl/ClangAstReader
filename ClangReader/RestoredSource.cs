using System;
using System.Collections.Generic;

namespace ClangReader.Types
{
    class TypeDeclaration
    {
        public bool isBuildIn;
        public bool isPointer;
        public string name;
    }

    class TypedefDeclaration
    {
        public string name;
        public TypeDeclaration alias;
    }

    class FunctionProtoDeclaration : TypeDeclaration
    {
        public List<TypeDeclaration> parameters = new List<TypeDeclaration>();
        public TypeDeclaration returnType;
    }

    class VariableDeclaration
    {
        public string name;
        public string type;
        public bool isStatic;
        public bool isExtern;
        public string value;
    }

    class FunctionDeclaration
    {
        public class Parameter
        {
            public string name;
            public string type;
            public string value;
        }

        public string name;
        public List<Parameter> parameters = new List<Parameter>();
        public string body;
    }

    class StructureDeclaration
    {
        public string name;
        public bool isClass;
        public bool isUnion;

        public class Property
        {
            public string name;
            public string type;
        }

        public List<Property> properties = new List<Property>();
        public List<StructureDeclaration> subStructures = new List<StructureDeclaration>();
        public List<string> others = new List<string>();
    }

    class EnumDeclaration
    {
        public string name;

        public class Property
        {
            public string name;
            public string value;
        }

        public List<Property> properties = new List<Property>();
    }
}
