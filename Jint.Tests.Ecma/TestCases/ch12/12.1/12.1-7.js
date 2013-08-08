/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.1/12.1-7.js
 * @description 12.1 - block '{ StatementListopt };' is not allowed: do-while
 */


function testcase() {
        try {
            eval("do{};while()");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
