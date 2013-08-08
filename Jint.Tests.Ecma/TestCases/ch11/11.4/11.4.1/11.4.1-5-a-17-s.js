/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-17-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a variable of type Arguments
 * @onlyStrict
 */


function testcase() {
        "use strict";
       try {
            eval("var argObj = (function (a, b) { delete arguments; }(1, 2));");

            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
