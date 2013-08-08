/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.3/7.3-10.js
 * @description 7.3 - ES5 recognizes the character <PS> (\u2029) as a NonEscapeCharacter
 */


function testcase() {
        try {
            eval("var prop = \\u2029;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
