/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.4/15.10.4.1/15.10.4.1-2.js
 * @description RegExp - the thrown error is SyntaxError instead of RegExpError when the characters of 'P' do not have the syntactic form Pattern
 */


function testcase() {
        try {
            var regExpObj = new RegExp('\\');

            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
