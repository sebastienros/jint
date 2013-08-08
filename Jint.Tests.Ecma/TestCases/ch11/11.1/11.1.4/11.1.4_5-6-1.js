/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 11.1.4; 
 * The production
 * ElementList : ElementList , Elisionopt AssignmentExpression
 * 6.Call the [[DefineOwnProperty]] internal method of array with arguments ToString(ToUint32((pad+len)) and the Property Descriptor { [[Value]]: initValue
 *     , [[Writable]]: true, [[Enumerable]]: true, [[Configurable]]: true}, and false.
 *
 * @path ch11/11.1/11.1.4/11.1.4_5-6-1.js
 * @description Initialize array using ElementList (ElementList , Elisionopt AssignmentExpression) when index property (read-only) exists in Array.prototype (step 6)
 */


function testcase() {
        try {
            Object.defineProperty(Array.prototype, "1", {
                value: 100,
                writable: false,
                configurable: true
            });
            var arr = [101, 12];

            return arr.hasOwnProperty("1") && arr[1] === 12;
        } finally {
            delete Array.prototype[1];
        }
    }
runTestCase(testcase);
