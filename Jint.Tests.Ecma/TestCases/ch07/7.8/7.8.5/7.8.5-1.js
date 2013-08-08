/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.8/7.8.5/7.8.5-1.js
 * @description Literal RegExp Objects - SyntaxError exception is thrown if the RegularExpressionNonTerminator position of a RegularExpressionBackslashSequence is a LineTerminator.
 */


function testcase() {
        try {
            eval("var regExp = /\\\rn/;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
