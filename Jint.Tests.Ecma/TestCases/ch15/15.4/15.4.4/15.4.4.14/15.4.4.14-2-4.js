/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-4.js
 * @description Array.prototype.indexOf - 'length' is own data property that overrides an inherited data property on an Array
 */


function testcase() {

        var targetObj = {};
        var arrProtoLen;

        try {
            arrProtoLen = Array.prototype.length;
            Array.prototype.length = 0;

            return [0, targetObj].indexOf(targetObj) === 1;

        } finally {

            Array.prototype.length = arrProtoLen;
        }
    }
runTestCase(testcase);
