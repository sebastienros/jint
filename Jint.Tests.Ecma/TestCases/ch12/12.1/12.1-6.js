/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.1/12.1-6.js
 * @description 12.1 - block '{ StatementListopt };' is not allowed: if-else-if-else
 */


function testcase() {
        try {
            eval("if{};else if{};else{}");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
