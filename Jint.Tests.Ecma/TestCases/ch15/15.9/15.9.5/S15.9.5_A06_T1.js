// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The Date.prototype has the property "toLocaleDateString"
 *
 * @path ch15/15.9/15.9.5/S15.9.5_A06_T1.js
 * @description The Date.prototype has the property "toLocaleDateString"
 */

if(Date.prototype.hasOwnProperty("toLocaleDateString") !== true){
  $ERROR('#1: The Date.prototype has the property "toLocaleDateString"');
}


