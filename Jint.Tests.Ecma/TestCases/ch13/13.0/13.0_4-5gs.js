/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.0/13.0_4-5gs.js
 * @description Strict Mode - SourceElements is evaluated as strict mode code when a FunctionDeclaration is contained in strict mode code
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */

"use strict";
throw NotEarlyError;
function _13_0_4_5_fun() { eval = 42; };
