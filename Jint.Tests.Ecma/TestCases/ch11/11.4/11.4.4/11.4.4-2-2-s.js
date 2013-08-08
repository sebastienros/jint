/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.4/11.4.4-2-2-s.js
 * @description Strict Mode - SyntaxError is thrown for ++arguments
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var blah = arguments;
        try {
            eval("++arguments;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError && blah === arguments;
        }
    }
runTestCase(testcase);
