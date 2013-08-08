/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.4/11.4.4-2-1-s.js
 * @description Strict Mode - SyntaxError is thrown for ++eval
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var blah = eval;
        try {
            eval("++eval;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError && blah === eval;
        }
    }
runTestCase(testcase);
