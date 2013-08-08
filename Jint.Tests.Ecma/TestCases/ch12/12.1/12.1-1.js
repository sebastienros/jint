/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.1/12.1-1.js
 * @description 12.1 - block '{ StatementListopt };' is not allowed: try-catch
 */


function testcase() {
        try {
            eval("try{};catch(){}");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
