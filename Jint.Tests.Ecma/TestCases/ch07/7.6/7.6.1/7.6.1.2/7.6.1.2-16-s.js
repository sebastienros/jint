/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-16-s.js
 * @description Strict Mode - SyntaxError isn't thrown when '_implements' occurs in strict mode code
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var _implements = 1;
        return _implements === 1;
    }
runTestCase(testcase);
