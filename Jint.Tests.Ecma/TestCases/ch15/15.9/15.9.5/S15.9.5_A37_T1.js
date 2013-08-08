// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The Date.prototype has the property "setUTCDate"
 *
 * @path ch15/15.9/15.9.5/S15.9.5_A37_T1.js
 * @description The Date.prototype has the property "setUTCDate"
 */

if(Date.prototype.hasOwnProperty("setUTCDate") !== true){
  $ERROR('#1: The Date.prototype has the property "setUTCDate"');
}


