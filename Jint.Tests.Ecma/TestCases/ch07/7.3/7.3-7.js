/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.3/7.3-7.js
 * @description 7.3 - ES5 recognizes the character <LS> (\u2028) as terminating regular expression literals
 */


function testcase() {
        try {
            eval("var regExp =  /[\u2028]/");
            regExp.test("");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
