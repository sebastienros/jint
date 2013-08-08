/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-8-s.js
 * @description StrictMode - enumerating over a function object looking for 'caller' fails inside the function
 * @onlyStrict
 */


function testcase() {
            var foo = new Function("'use strict'; for (var tempIndex in this) {if (tempIndex===\"caller\") {return false;}}; return true;");
            return foo();
    }
runTestCase(testcase);
