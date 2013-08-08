/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-14-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a variable of type Date
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var dateObj = new Date();

        try {
            eval("delete dateObj;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
