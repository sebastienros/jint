/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.5/10.5-1-s.js
 * @description Strict Mode - arguments object is immutable
 * @onlyStrict
 */


function testcase() {
        "use strict";
        try {
            (function fun() {
                eval("arguments = 10");
            })(30);
            return false;
        } catch (e) {
            return (e instanceof SyntaxError);
        }
    }
runTestCase(testcase);
