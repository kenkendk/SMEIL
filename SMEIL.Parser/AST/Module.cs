﻿using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a module
    /// </summary>
    public class Module : ParsedItem
    {
        /// <summary>
        /// The list of import statements
        /// </summary>
        public readonly ImportStatement[] Imports;

        /// <summary>
        /// The entities in the module
        /// </summary>
        public readonly Entity[] Entities;

        /// <summary>
        /// The type definitions in the module
        /// </summary>
        public readonly Declaration[] Declarations;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Module"/> class.
        /// </summary>
        /// <param name="token">The source token</param>
        /// <param name="imports">The imports in the module.</param>
        /// <param name="declarations">The typedefinitions in the module.</param>
        /// <param name="entities">The entities in the module.</param>
        public Module(ParseToken token, ImportStatement[] imports, Declaration[] declarations, Entity[] entities)
            : base(token)
        {
            Imports = imports;
            Entities = entities;
            Declarations = declarations;
        }
    }
}
