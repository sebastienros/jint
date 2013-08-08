/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.1/12.1-2.js
 * @description 12.1 - block '{ StatementListopt };' is not allowed: try-catch-finally
 */


function testcase() {
        try {
            eval("try{};catch{};finally{}");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
