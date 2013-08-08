/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-21.js
 * @description Array.prototype.map - 'length' is an object that has an own valueOf method that returns an object and toString method that returns a string
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val < 10;
        }

        var firstStepOccured = false;
        var secondStepOccured = false;
        var obj = {
            0: 11,
            1: 9,

            length: {
                valueOf: function () {
                    firstStepOccured = true;
                    return {};
                },
                toString: function () {
                    secondStepOccured = true;
                    return '2';
                }
            }
        };

        var newArr = Array.prototype.map.call(obj, callbackfn);

        return newArr.length === 2 && firstStepOccured && secondStepOccured;
    }
runTestCase(testcase);
