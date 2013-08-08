/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-5-a-28-s.js
 * @description Strict Mode - TypeError is thrown when deleting RegExp.length
 * @onlyStrict
 */


function testcase() {
    "use strict";
    var a = new RegExp();
    try {
        var b = delete RegExp.length;
        return false;
    } catch (e) {
        return e instanceof TypeError;
    }
}
runTestCase(testcase);
