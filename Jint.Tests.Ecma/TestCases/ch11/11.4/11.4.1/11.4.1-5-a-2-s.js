/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-2-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a function parameter
 * @onlyStrict
 */


function testcase() {
        "use strict";
        function funObj(x) {
            eval("delete x;");
        }

        try {
            funObj(1);
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
