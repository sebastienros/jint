/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.4/15.10.4.1/15.10.4.1-3.js
 * @description RegExp - the thrown error is SyntaxError instead of RegExpError when 'F' contains any character other than 'g', 'i', or 'm' 
 */


function testcase() {
        try {
            var regExpObj = new RegExp('abc', 'a');

            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
