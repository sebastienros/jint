/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.5/11.4.5-2-2gs.js
 * @description Strict Mode - SyntaxError is throw if the UnaryExpression operated upon by a Prefix Increment operator(--arguments)
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */

"use strict";
throw NotEarlyError;
--arguments;
