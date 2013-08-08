/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.4/11.4.4-2-3-s.js
 * @description Strict Mode - SyntaxError is not thrown for ++arguments[...]
 * @onlyStrict
 */


function testcase() {
        "use strict";
        arguments[1] = 7;
        ++arguments[1];
        return arguments[1]===8;
    }
runTestCase(testcase);
