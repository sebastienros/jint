/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-14-1-s.js
 * @description Strict Mode - 'callee' exists and 'caller' exists under strict mode
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var argObj = function () {
            return arguments;
        } ();
        return argObj.hasOwnProperty("callee") && argObj.hasOwnProperty("caller");
    }
runTestCase(testcase);
