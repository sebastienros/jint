/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.11/15.5.4.11-1.js
 * @description 'this' object used by the replaceValue function of a String.prototype.replace invocation
 */




function testcase() {
  var retVal = 'x'.replace(/x/, 
                           function() { 
                               if (this===fnGlobalObject()) {
                                   return 'y';
                               } else {
                                   return 'z';
                               }
                           });
  return retVal==='y';
}
runTestCase(testcase);