/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-13-s.js
 * @description Strict Mode - SyntaxError is thrown when deleting a variable of type Number
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var numObj = new Number(0);

        try {
            eval("delete numObj;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
