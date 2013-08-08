/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.3/15.3.4.3-1-s.js
 * @description Strict Mode - 'this' value is a string which cannot be converted to wrapper objects when the function is called with an array of arguments
 * @onlyStrict
 */


function testcase() {
        "use strict";

        function fun() {
            return (this instanceof String);
        }
        return !fun.apply("", Array);
    }
runTestCase(testcase);
