/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.3/7.3-3.js
 * @description 7.3 - ES5 recognizes the character <LS> (\u2028) as terminating SingleLineComments
 */


function testcase() {
        try {
            eval("//Single Line Comments\u2028 var =;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
