/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.2/11.13.2-15-s.js
 * @description Strict Mode - ReferenceError isn't thrown if the LeftHandSideExpression of a Compound Assignment operator(>>>=) evaluates to a resolvable reference
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var _11_13_2_15 = 8
        _11_13_2_15 >>>= 2;
        return _11_13_2_15 === 2;
    }
runTestCase(testcase);
