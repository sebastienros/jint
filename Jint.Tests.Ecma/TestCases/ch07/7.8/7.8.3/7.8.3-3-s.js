/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.8/7.8.3/7.8.3-3-s.js
 * @description Strict Mode - octal extension (01) is forbidden in strict mode
 * @onlyStrict
 */


function testcase() {
        "use strict";
        try {
            eval("var _7_8_3_3 = 01;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError && typeof _7_8_3_3 === "undefined";
        }
    }
runTestCase(testcase);
