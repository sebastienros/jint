/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-3-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a function name
 * @onlyStrict
 */


function testcase() {
        "use strict";
        function funObj () { }

        try {
            eval("delete funObj");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
