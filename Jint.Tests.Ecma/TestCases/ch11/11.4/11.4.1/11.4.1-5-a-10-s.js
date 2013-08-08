/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-10-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a variable of type Array
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var arrObj = [1,2,3];

        try {
            eval("delete arrObj;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
