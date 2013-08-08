/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-11-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a variable of type String
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var strObj = new String("abc");

        try {
            eval("delete strObj;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
