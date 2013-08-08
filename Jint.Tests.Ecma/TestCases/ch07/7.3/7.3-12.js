/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.3/7.3-12.js
 * @description 7.3 - ES5 specifies that a multiline comment that contains a line terminator character <PS> (\u2029) must be treated as a single line terminator for the purposes of semicolon insertion
 */


function testcase() {
        /*MultiLine
        Comments 
        \u2029 var = ;
        */
        return true;
    }
runTestCase(testcase);
