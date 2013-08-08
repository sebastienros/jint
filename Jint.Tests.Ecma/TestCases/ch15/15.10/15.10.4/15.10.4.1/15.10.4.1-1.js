/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.4/15.10.4.1/15.10.4.1-1.js
 * @description RegExp - the thrown error is TypeError instead of RegExpError when pattern is an object whose [[Class]] property is 'RegExp' and flags is not undefined 
 */


function testcase() {
        var regObj = new RegExp();
        try {
            var regExpObj = new RegExp(regObj, true);

            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);
