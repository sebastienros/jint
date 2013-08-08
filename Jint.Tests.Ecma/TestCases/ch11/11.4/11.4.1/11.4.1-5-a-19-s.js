/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-19-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a built-in (Function)
 * @onlyStrict
 */


function testcase() {
        "use strict";

        try {
            eval("delete Function;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
