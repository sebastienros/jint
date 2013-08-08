/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.2/11.13.2-6-1gs.js
 * @description Strict Mode - SyntaxError is throw if the identifier eval appears as the LeftHandSideExpression of a Compound Assignment operator(*=)
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */

"use strict";
throw NotEarlyError;
eval *= 20;
