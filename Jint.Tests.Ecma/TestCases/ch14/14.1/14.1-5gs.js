/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-5gs.js
 * @description StrictMode - a Use Strict Directive embedded in a directive prologue followed by a strict mode violation
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */
"a";
"use strict";
"c";
throw NotEarlyError;
eval = 42;