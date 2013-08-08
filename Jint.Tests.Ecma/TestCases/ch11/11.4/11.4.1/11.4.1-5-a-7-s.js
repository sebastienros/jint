/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-7-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a variable of type Object
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var obj = new Object();

        try {
            eval("delete obj;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
