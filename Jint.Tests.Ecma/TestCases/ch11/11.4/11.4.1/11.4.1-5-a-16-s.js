/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-16-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a variable of type Error
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var errObj = new Error();

        try {
            eval("delete errObj;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
