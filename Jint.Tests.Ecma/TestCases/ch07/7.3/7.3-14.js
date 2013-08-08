/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.3/7.3-14.js
 * @description 7.3 - ES5 specifies that a multiline comment that contains a line terminator character <LF> (\u000A) must be treated as a single line terminator for the purposes of semicolon insertion
 */


function testcase() {
        /*MultiLine
        Comments 
        \u000A var = ;
        */
        return true;
    }
runTestCase(testcase);
