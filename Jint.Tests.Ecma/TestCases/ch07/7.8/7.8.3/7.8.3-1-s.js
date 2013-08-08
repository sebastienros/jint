/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.8/7.8.3/7.8.3-1-s.js
 * @description Strict Mode - octal extension (010) is forbidden in strict mode
 * @onlyStrict
 */


function testcase() {
        "use strict";
        try {           
            eval("var _7_8_3_1 = 010;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError && typeof _7_8_3_1 === "undefined";
        }
    }
runTestCase(testcase);
