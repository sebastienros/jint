/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-6-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a variable which is a primitive type (string)
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var _11_4_1_5 = "abc";

        try {
            eval("delete _11_4_1_5;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
