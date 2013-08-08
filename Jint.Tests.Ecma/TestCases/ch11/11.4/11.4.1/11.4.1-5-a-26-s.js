/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-26-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a built-in (Error)
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var errorBackup = Error;
        try {
            eval("delete Error;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        } finally {
            Error = errorBackup;
        }
        
    }
runTestCase(testcase);
